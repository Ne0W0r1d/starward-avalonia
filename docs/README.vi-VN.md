[English](../README.md) | [简体中文](./README.zh-CN.md) | **Tiếng Việt** | [日本語](./README.ja-JP.md) | [ภาษาไทย](./README.th-TH.md) | [Русский](./README.ru-RU.md)


# Starward-Avalonia

> **Starward** bắt nguồn từ khẩu hiệu của Star Rail: May This Journey Lead Us **Starward** (Chuyến đi này sẽ đưa chúng ta tới những vì sao), rất phù hợp để đặt làm tên ứng dụng.


> [!WARNING]
> Dự án này là bản fork hạ nguồn của Starward, sẽ được chuyển sang Avalonia và phục vụ người dùng Linux.


Starward-Avalonia là một trình khởi chạy bên thứ ba mã nguồn mở, được phát triển nhằm khắc phục những hạn chế của HoYoPlay (trình khởi chạy miHoYo). Ứng dụng hỗ trợ toàn bộ game PC của HoYoverse và hướng tới việc thay thế hoàn toàn trình khởi chạy chính thức trên PC Linux và Steam Deck. Ngoài các chức năng cơ bản của một trình khởi chạy, tôi còn bổ sung một số tính năng theo nhu cầu cá nhân, chẳng hạn như:

- Ghi lại thời gian chơi game
- Chuyển đổi tài khoản game
- Xem ảnh chụp màn hình trong game
- Lưu lịch sử gacha

Còn nhiều tính năng khác đang chờ bạn khám phá...


## Cài đặt

Trước tiên, thiết bị của bạn cần đáp ứng các yêu cầu sau:

- Một bản phân phối Linux có thể cài đặt .NET 10
- Đã cài đặt WebKit2GTK


Tiếp theo, hãy tải gói phù hợp với kiến trúc CPU của bạn từ [GitHub Release](https://github.com/Ne0W0r1d/Starward-avalonia/releases). Giải nén, sau đó cài đặt hoặc chạy và làm theo hướng dẫn.


## Phát triển

Hãy cài đặt [.NET 10 SDK](https://dotnet.microsoft.com/download). Sau đó biên dịch và chạy từ thư mục gốc của kho lưu trữ:

```bash
dotnet build Starward.Avalonia.slnx
dotnet run --project src/Starward.Avalonia
```

Để chỉnh sửa mã nguồn, bạn có thể dùng VS Code / VSCodium (cài extension C#) hoặc JetBrains Rider.


## Quyên góp

Phát triển phần mềm không hề dễ dàng. Nếu bạn thấy Starward hữu ích:

- Bạn có thể quyên góp cho dự án gốc tại https://donate.scighost.com.
- Hoặc quyên góp token của tôi tại https://0w0.red/sponsor/.





## Ảnh chụp màn hình
