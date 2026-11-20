using Dapper;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Database;
using Starward.Features.GameLauncher;
using Starward.Features.HoYoPlay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.PlayTime;

public sealed class PlayTimeRecordService
{

    private readonly ILogger<PlayTimeRecordService> _logger;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly PlayTimeStatsService _playTimeStatsService;


    public PlayTimeRecordService(ILogger<PlayTimeRecordService> logger, HoYoPlayService hoYoPlayService, PlayTimeStatsService playTimeStatsService)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _playTimeStatsService = playTimeStatsService;
    }


    /// <summary>
    /// 游戏启动后等待真正的游戏进程出现，然后启动独立的记录进程。
    /// </summary>
    public async Task TrackLaunchedGameAsync(GameBiz biz, Process launchedProcess)
    {
        try
        {
            string name = await GetGameExeNameAsync(biz);
            int exitedChecks = 0;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1000);
                Process? game = FindProcessByExeName(name);
                if (game is not null)
                {
                    StartMonitorProcess(biz, game.Id);
                    return;
                }
                try
                {
                    if (launchedProcess.HasExited)
                    {
                        exitedChecks++;
                        if (exitedChecks >= 10)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Track launched game {biz}", biz);
        }
    }


    public void StartMonitorProcess(GameBiz biz, int pid)
    {
        try
        {
            string lockFile = Path.Combine(AppConfig.CacheFolder, $"playtime_{pid}.lock");
            if (File.Exists(lockFile))
            {
                try
                {
                    using FileStream fs = new(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    fs.SetLength(0);
                    fs.Write(BitConverter.GetBytes(Environment.ProcessId));
                }
                catch
                {
                    _logger.LogDebug("Playtime of pid {pid} is already recorded", pid);
                    return;
                }
            }
            ProcessStartInfo info = AppConfig.CreateSelfStartInfo("log-playtime", biz.Value, pid.ToString());
            info.CreateNoWindow = true;
            Process.Start(info);
            _logger.LogInformation("Start playtime monitor: {biz}, pid {pid}", biz, pid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start playtime monitor: {biz}, pid {pid}", biz, pid);
        }
    }


    public async Task LogPlayTimeAsync(GameBiz biz, int pid)
    {
        string lockFile = Path.Combine(AppConfig.CacheFolder, $"playtime_{pid}.lock");
        FileStream? lockStream = null;
        bool ownsLock = false;
        try
        {
            biz = biz.IsBilibili() ? $"{biz.Game}_cn" : biz;
            Directory.CreateDirectory(AppConfig.CacheFolder);
            lockStream = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            ownsLock = true;
            lockStream.SetLength(0);
            lockStream.Write(BitConverter.GetBytes(Environment.ProcessId));
            lockStream.Flush();

            Process process = Process.GetProcessById(pid);
            LogStartState(biz, process);
            Stopwatch sw = Stopwatch.StartNew();
            long last = 0;
            while (true)
            {
                await Task.Delay(Random.Shared.Next(800, 1200));
                if (process.HasExited)
                {
                    DateTimeOffset now = DateTimeOffset.Now;
                    Log(biz, pid, PlayTimeState.Stop, now.ToUnixTimeMilliseconds(), $"{process.ProcessName} [{now}]");
                    SavePlayTimeStats(biz, pid, process.StartTime, now.DateTime);
                    break;
                }
                if (sw.ElapsedMilliseconds - last > 30000)
                {
                    Log(biz, pid, PlayTimeState.Play);
                    last = sw.ElapsedMilliseconds;
                }
            }
            DatabaseService.SetValue($"playtime_total_{biz}", _playTimeStatsService.GetPlayTimeTotal(biz));
            _logger.LogInformation("End log playtime ({biz}, {pid})", biz, pid);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Play time of pid {pid} is already being recorded", pid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log play time: GameBiz {biz}, Pid {pid}", biz, pid);
        }
        finally
        {
            try { lockStream?.Dispose(); } catch { }
            if (ownsLock)
            {
                try { File.Delete(lockFile); } catch { }
            }
        }
    }


    private void LogStartState(GameBiz biz, Process process)
    {
        DateTimeOffset startTime = new(process.StartTime);
        Log(biz, process.Id, PlayTimeState.Start, startTime.ToUnixTimeMilliseconds(), $"{process.ProcessName} [{startTime}]");
        using var dapper = DatabaseService.CreateConnection();
        PlayTimeItemStruct last = dapper.QueryFirstOrDefault<PlayTimeItemStruct>(
            "SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND Pid = @Id ORDER BY TimeStamp DESC LIMIT 1;",
            new { biz = biz.Value, process.Id });
        DateTimeOffset time = startTime;
        if (last.TimeStamp > startTime.ToUnixTimeMilliseconds())
        {
            time = DateTimeOffset.FromUnixTimeMilliseconds(last.TimeStamp);
        }
        DateTimeOffset now = DateTimeOffset.Now;
        if (now - time >= TimeSpan.FromSeconds(60))
        {
            List<PlayTimeItem> list = new();
            while (true)
            {
                time = time.AddMilliseconds(Random.Shared.Next(30_000, 32_000));
                if (time < now)
                {
                    list.Add(new PlayTimeItem
                    {
                        TimeStamp = time.ToUnixTimeMilliseconds(),
                        GameBiz = biz,
                        Pid = process.Id,
                        State = PlayTimeState.Play,
                    });
                }
                else
                {
                    break;
                }
            }
            if (list.Count > 0)
            {
                using var t = dapper.BeginTransaction();
                dapper.Execute("INSERT OR REPLACE INTO PlayTimeItem (TimeStamp, GameBiz, Pid, State, CursorPos, Message) VALUES (@TimeStamp, @GameBiz, @Pid, @State, @CursorPos, @Message);", list, t);
                t.Commit();
            }
        }
    }


    private void Log(GameBiz biz, int pid, PlayTimeState state, long ts = 0, string? message = null)
    {
        try
        {
            using var dapper = DatabaseService.CreateConnection();
            PlayTimeItem item = new()
            {
                TimeStamp = ts == 0 ? DateTimeOffset.Now.ToUnixTimeMilliseconds() : ts,
                GameBiz = biz,
                Pid = pid,
                State = state,
                Message = message,
            };
            dapper.Execute("INSERT OR REPLACE INTO PlayTimeItem (TimeStamp, GameBiz, Pid, State, CursorPos, Message) VALUES (@TimeStamp, @GameBiz, @Pid, @State, @CursorPos, @Message);", item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log play time: GameBiz {biz}, Pid {pid}, State {state}", biz, pid, state);
        }
    }


    private void SavePlayTimeStats(GameBiz biz, int pid, DateTime startTime, DateTime endTime)
    {
        try
        {
            using var dapper = DatabaseService.CreateConnection();
            PlayTimeStats stats = new()
            {
                GameBiz = biz,
                Pid = pid,
                StartTime = new DateTimeOffset(startTime).ToUnixTimeMilliseconds(),
                EndTime = new DateTimeOffset(endTime).ToUnixTimeMilliseconds(),
            };
            using var t = dapper.BeginTransaction();
            dapper.Execute("INSERT OR REPLACE INTO PlayTimeStats (GameBiz, Pid, StartTime, EndTime, Interruption, Type) VALUES (@GameBiz, @Pid, @StartTime, @EndTime, @Interruption, @Type);", stats);
            dapper.Execute("DELETE FROM PlayTimeItem WHERE GameBiz = @GameBiz AND Pid = @Pid AND TimeStamp >= @StartTime AND TimeStamp <= @EndTime;", stats);
            t.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save play time stats: {biz}, pid {pid}", biz, pid);
        }
    }


    public async Task<string> GetGameExeNameAsync(GameBiz biz)
    {
        string? name = GameLauncherService.GetGameExeName(biz);
        if (string.IsNullOrWhiteSpace(name))
        {
            GameId? gameId = Core.HoYoPlay.GameId.FromGameBiz(biz);
            if (gameId is not null)
            {
                var config = await _hoYoPlayService.GetGameConfigAsync(gameId);
                name = config?.ExeFileName;
            }
        }
        return name ?? throw new ArgumentOutOfRangeException(Localization.Format("Avalonia_Error_UnknownGame", biz));
    }


    private static Process? FindProcessByExeName(string exeName)
    {
        return GameLauncherService.FindGameProcess(exeName);
    }

}
