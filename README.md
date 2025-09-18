# Cobblemon One-Click Launcher (.NET App)

Ứng dụng Launcher độc lập viết bằng **.NET (C# / WPF)** dành riêng cho modpack **Cobblemon Fabric** trên **Minecraft 1.21.1**, tích hợp sẵn **9 mod/datapack mở rộng**, quản lý **Java 21 Portable**, và giao diện Menu chính được tùy biến chỉ còn **2 nút duy nhất**: **"Start game"** (tự kết nối vào Server) và **"Cài đặt"**.

---

## 🌟 Tính Năng Nổi Bật

1. **Khởi chạy One-Click:**
   - Người chơi chỉ cần nhấn **"CHƠI NGAY"**, ứng dụng sẽ tự động tải các file còn thiếu và vào thẳng game.
2. **Tự động quản lý Java 21 Portable:**
   - Ứng dụng tự phát hiện Java 21 trên máy.
   - Nếu chưa có, ứng dụng tự động tải bản nén **Eclipse Temurin OpenJDK 21 (x64)** và giải nén vào thư mục `instance/runtime/java-21`, không yêu cầu người dùng phải tự cài đặt Java.
3. **Base Modpack & Tự đồng bộ 9 Mod Modrinth:**
   - Base modpack: **`Cobblemon Official Modpack [Fabric]`** 1.8.1 (chứa sẵn Cobblemon, Fabric API, Sodium, Lithium, FerriteCore, EMI, Xaero's Maps, FancyMenu...).
   - Tự động tải và đồng bộ 9 mod / datapack bổ sung:
     1. **Kazeran Eeveelutions** (`kazeran-eeveelutions`)
     2. **Cobblemon Paleontology** (`cobblemon-paleontology`)
     3. **Rillaboom Wood Variants** (`rillaboom-wood-variant-cosmetics-cobblemon`)
     4. **Cobblemon Pokestops** (`cobblemon-pokestops`)
     5. **Cobblemon Expeditions** (`cobblemon_expeditions`)
     6. **W's Angry Alphas** (`ws-angry-alphas`)
     7. **Pokebelt** (`pokebelt-cobblemon`)
     8. **Cobblemon Trainers** (`cobblemon-trainers`)
     9. **Cobblemon Trainer Pass** (`trainer-pass`)
4. **Main Menu 2 Nút Tùy Biến (FancyMenu):**
   - Ẩn toàn bộ các nút mặc định (Singleplayer, Multiplayer, Realms, Mods...).
   - **Nút 1 ("Start game"):** Tự động kết nối trực tiếp đến địa chỉ IP Server Minecraft đã được cấu hình.
   - **Nút 2 ("Cài đặt"):** Mở màn hình Options/Settings của Minecraft.
5. **Hỗ trợ 2 chế độ tài khoản:**
   - **Chơi Offline:** Nhập tên nhân vật bất kỳ để vào chơi server offline/nội bộ.
   - **Tài khoản Microsoft:** Đăng nhập an toàn qua OAuth Device Code Flow (hiển thị mã và mở trình duyệt để xác thực).
6. **Tùy biến RAM & Server IP:**
   - Thanh trượt chỉnh RAM cấp phát từ 2GB đến 16GB (mặc định 4GB - 6GB).
   - Ô nhập địa chỉ Server (lưu vào `config.json` ngay cạnh launcher).

---

## 🚀 Cách Chạy Ứng Dụng

- **Cách 1 (Nhanh nhất):** Nhấp đúp vào file `Chạy Cobblemon Launcher.bat` ở thư mục này.
- **Cách 2:** Mở thư mục `ReleaseApp` và chạy trực tiếp file `CobblemonLauncher.exe`.
- **Cách 3 (Dành cho lập trình viên):**
  ```powershell
  cd CobblemonLauncher
  dotnet run
  ```

---

## 📁 Cấu Trúc Thư Mục

```text
Đá cuội mon/
├── Chạy Cobblemon Launcher.bat       # Phím tắt mở nhanh launcher
├── ReleaseApp/
│   ├── CobblemonLauncher.exe         # File thực thi ứng dụng .NET đã biên dịch
│   └── config.json                   # Lưu cấu hình: Server IP, RAM, Tên người chơi
├── CobblemonLauncher/                # Mã nguồn dự án C# WPF .NET
│   ├── MainWindow.xaml               # Giao diện Dark Theme hiện đại
│   ├── MainWindow.xaml.cs            # Logic điều khiển giao diện & tiến trình
│   ├── Models/AppConfig.cs           # Model lưu cấu hình
│   └── Services/
│       ├── JavaService.cs            # Tự động tải & quản lý Java 21 Portable
│       ├── ModpackService.cs         # Tải modpack và 9 mod từ Modrinth API
│       ├── FancyMenuService.cs       # Cấu hình Main Menu 2 nút
│       ├── AuthService.cs            # Quản lý Offline & Microsoft OAuth
│       └── MinecraftLauncherService.cs # Tải game client & khởi chạy tiến trình
└── README.md
```
