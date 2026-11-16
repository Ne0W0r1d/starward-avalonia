[English](../README.md) | [简体中文](./README.zh-CN.md) | [Tiếng Việt](./README.vi-VN.md) | [日本語](./README.ja-JP.md) | **ภาษาไทย** | [Русский](./README.ru-RU.md)


# Starward-Avalonia

> **Starward** มาจากสโลแกนของ Star Rail: May This Journey Lead Us **Starward** (ขอให้การเดินทางครั้งนี้พาเรามุ่งไปสู่ดวงดาว) ซึ่งเหมาะมากที่จะใช้เป็นชื่อแอป


> [!WARNING]
> โปรเจกต์นี้เป็น downstream fork ของ Starward ซึ่งจะพอร์ตไปยัง Avalonia และให้บริการสำหรับผู้ใช้ Linux


Starward-Avalonia เป็นตัวเปิดเกม (launcher) แบบโอเพนซอร์สจากบุคคลที่สาม พัฒนาขึ้นเพื่อแก้ไขข้อบกพร่องของ HoYoPlay (launcher ของ miHoYo) โดยรองรับเกม PC ทั้งหมดของ HoYoverse และมีเป้าหมายที่จะแทนที่ launcher อย่างเป็นทางการบน Linux PC และ Steam Deck อย่างสมบูรณ์ นอกเหนือจากฟังก์ชันพื้นฐานของ launcher แล้ว ผมยังเพิ่มฟีเจอร์บางอย่างตามความต้องการส่วนตัว เช่น:

- บันทึกเวลาการเล่นเกม
- สลับบัญชีเกม
- ดูภาพหน้าจอในเกม
- บันทึกประวัติการกาชา

ยังมีฟีเจอร์อื่น ๆ อีกมากให้คุณได้ลองค้นหา...


## การติดตั้ง

ก่อนอื่น อุปกรณ์ของคุณต้องมีคุณสมบัติตามข้อกำหนดต่อไปนี้:

- ดิสทริบิวชัน Linux ที่ติดตั้ง .NET 10 ได้
- ติดตั้ง WebKit2GTK แล้ว


จากนั้นดาวน์โหลดแพ็กเกจสำหรับสถาปัตยกรรม CPU ของคุณจาก [GitHub Release](https://github.com/Ne0W0r1d/Starward-avalonia/releases) แตกไฟล์ แล้วติดตั้งหรือเรียกใช้งานตามคำแนะนำ


## การพัฒนา

ติดตั้ง [.NET 10 SDK](https://dotnet.microsoft.com/download) จากนั้นคอมไพล์และรันจากโฟลเดอร์รากของรีโพซิทอรี:

```bash
dotnet build Starward.Avalonia.slnx
dotnet run --project src/Starward.Avalonia
```

สำหรับการแก้ไขโค้ด คุณสามารถใช้ VS Code / VSCodium (พร้อมส่วนขยาย C#) หรือ JetBrains Rider


## การสนับสนุน

การพัฒนาไม่ใช่เรื่องง่าย หากคุณคิดว่า Starward มีประโยชน์

- คุณสามารถสนับสนุนโปรเจกต์ต้นทางได้ที่ https://donate.scighost.com
- หรือสนับสนุน token ของผมได้ที่ https://0w0.red/sponsor/





## ภาพหน้าจอ
