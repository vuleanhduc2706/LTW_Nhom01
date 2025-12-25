USE master;
GO

-- 1. Xóa Database cũ nếu đang tồn tại để làm sạch dữ liệu
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'BanhNgot')
BEGIN
    ALTER DATABASE BanhNgot SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE BanhNgot;
END
GO

-- 2. Tạo mới Database
CREATE DATABASE BanhNgot;
GO

USE BanhNgot;
GO

---------------------------------------------------------
-- CẤU TRÚC BẢNG
---------------------------------------------------------

-- Bảng Danh Mục
CREATE TABLE dbo.DanhMuc (
    MaDanhMuc     VARCHAR(20) PRIMARY KEY,      
    Ten           NVARCHAR(100) NOT NULL UNIQUE,
    MoTa          NVARCHAR(255) NULL,
    TrangThai     BIT NOT NULL CONSTRAINT DF_DanhMuc_TrangThai DEFAULT(1)  
);
GO

-- Bảng Người Dùng
CREATE TABLE dbo.NguoiDung (
    MaND          VARCHAR(20) PRIMARY KEY,        
    HoTen         NVARCHAR(100) NOT NULL,
    SDT           VARCHAR(15) NOT NULL UNIQUE,
    Email         VARCHAR(100) NOT NULL UNIQUE,
	DiaChi		  NVARCHAR(255),
    MatKhau       NVARCHAR(100) NOT NULL, 
    QuyenHan      NVARCHAR(50) NOT NULL 
                  CONSTRAINT CK_NguoiDung_Quyen CHECK (QuyenHan IN (N'Admin', N'NguoiBan', N'NguoiMua')),
    TrangThai     INT DEFAULT 1 
                  CONSTRAINT CK_NguoiDung_TrangThai CHECK (TrangThai IN (0, 1, 2))
);
GO

-- Bảng Cửa Hàng
CREATE TABLE dbo.CuaHang (
    MaCuaHang     VARCHAR(20) PRIMARY KEY,
    TenCuaHang    NVARCHAR(100) NOT NULL,
    MaND          VARCHAR(20) UNIQUE, 
    TrangThaiCH   INT DEFAULT 1,
	DiaChi NVARCHAR(255),
    CONSTRAINT FK_CuaHang_NguoiDung FOREIGN KEY (MaND) REFERENCES dbo.NguoiDung(MaND)
);
GO  

-- Bảng Sản Phẩm
CREATE TABLE dbo.SanPham (
    MaSanPham     VARCHAR(30) PRIMARY KEY,      
    Ten           NVARCHAR(150) NOT NULL,
    MaDanhMuc     VARCHAR(20) NOT NULL,
    MaCuaHang     VARCHAR(20) NOT NULL, 
    GiaBan        DECIMAL(12,2) NOT NULL,     
    DonViTinh     NVARCHAR(20) NOT NULL CONSTRAINT DF_SanPham_DVT DEFAULT (N'cái'),
    TrangThai     NVARCHAR(20) NOT NULL CONSTRAINT DF_SanPham_TrangThai DEFAULT (N'Đang bán'),
    MoTa          NVARCHAR(255) NULL,
    HinhAnh       VARCHAR(500) NULL, 
    SoLuongTon    INT DEFAULT 0,

    CONSTRAINT FK_SanPham_DanhMuc FOREIGN KEY (MaDanhMuc) REFERENCES dbo.DanhMuc(MaDanhMuc),
    CONSTRAINT FK_SanPham_CuaHang FOREIGN KEY (MaCuaHang) REFERENCES dbo.CuaHang(MaCuaHang),
    CONSTRAINT UQ_SanPham_MaDanhMuc_Ten UNIQUE (MaDanhMuc, Ten) 
);
GO

-- Bảng Đơn Hàng
CREATE TABLE dbo.DonHang (
    MaDon          VARCHAR(30) PRIMARY KEY,      
    MaKhachHang    VARCHAR(20) NOT NULL,
    NgayDat        DATETIME2 NOT NULL CONSTRAINT DF_DH_Ngay DEFAULT (SYSDATETIME()),
    TrangThai      NVARCHAR(20) NOT NULL CONSTRAINT DF_DH_TrangThai DEFAULT (N'Chờ xử lý'),
    TongTien       DECIMAL(14,2) NOT NULL,    
    GhiChu         NVARCHAR(255) NULL,
	TenNguoiNhan   NVARCHAR(100),
	SDTNguoiNhan   VARCHAR(15),
	DiaChiGiaoHang NVARCHAR(255),
    CONSTRAINT FK_DH_ND FOREIGN KEY (MaKhachHang) REFERENCES dbo.NguoiDung(MaND)
);
GO

-- Bảng Chi Tiết Đơn Hàng
CREATE TABLE dbo.ChiTietDonHang (
    MaDon         VARCHAR(30) NOT NULL,
    MaSanPham     VARCHAR(30) NOT NULL,
    SoLuong       INT NOT NULL,
    DonGia        DECIMAL(12,2) NOT NULL,
    GiamGia       DECIMAL(12,2) NOT NULL CONSTRAINT DF_CTDH_GG DEFAULT(0),
    ThanhTien     AS (ROUND((SoLuong * DonGia) - GiamGia, 2)) PERSISTED,
    
    PRIMARY KEY (MaDon, MaSanPham),
    CONSTRAINT FK_CTDH_DH FOREIGN KEY (MaDon) REFERENCES dbo.DonHang(MaDon),
    CONSTRAINT FK_CTDH_SP FOREIGN KEY (MaSanPham) REFERENCES dbo.SanPham(MaSanPham),
    CONSTRAINT CK_CTDH_SoLuong CHECK (SoLuong > 0),
    CONSTRAINT CK_CTDH_DonGia CHECK (DonGia >= 0),
    CONSTRAINT CK_CTDH_GiamGia CHECK (GiamGia >= 0)
);
GO

---------------------------------------------------------
-- TRIGGERS
---------------------------------------------------------

-- Trigger kiểm tra trạng thái sản phẩm khi mua
CREATE TRIGGER dbo.Trg_KiemTraTrangThaiSanPham
ON dbo.ChiTietDonHang
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM inserted i 
        JOIN dbo.SanPham sp ON sp.MaSanPham = i.MaSanPham
        WHERE sp.TrangThai <> N'Đang bán'
    )
    BEGIN
        RAISERROR(N'Không thể bán sản phẩm đã ngừng kinh doanh!', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

-- Trigger kiểm tra giảm giá hợp lệ
CREATE TRIGGER dbo.Trg_KiemTraGiamGiaHopLe
ON dbo.ChiTietDonHang
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE GiamGia > (SoLuong * DonGia))
    BEGIN
        RAISERROR(N'Số tiền giảm giá không được vượt quá tổng giá trị sản phẩm.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

---------------------------------------------------------
-- DỮ LIỆU MẪU (Dữ liệu Insert)
---------------------------------------------------------

-- 1. Insert Danh mục
INSERT INTO dbo.DanhMuc (MaDanhMuc, Ten, MoTa) VALUES  
('DMC001', N'Bánh Mì', N'Nhóm sản phẩm bánh mì'),
('DMC002', N'Bánh Ngọt', N'Các loại bánh ngọt như bánh kem, bánh quy'),
('DMC003', N'Bánh Sinh Nhật', N'Bánh sinh nhật cho các dịp lễ'),
('DMC008', N'Bánh Cupcake', N'Các loại cupcake đa dạng'),
('DMC009', N'Bánh Âu', N'Các loại bánh Âu như croissant');
GO

-- 2. Insert Người dùng (Bao gồm Admin, Người Bán, Người Mua)
INSERT INTO dbo.NguoiDung (MaND, HoTen, SDT, Email, MatKhau, QuyenHan, TrangThai) VALUES
('AD001', N'Lê Quản Trị', '0900000001', 'admin@banhngot.com', 'admin123', N'Admin', 1),
('NB001', N'Nguyễn Thu Hà', '0911000001', 'thuha.bakery@gmail.com', '123456', N'NguoiBan', 1),
('NB002', N'Trần Minh Tâm', '0911000002', 'minhtam.pastry@gmail.com', '123456', N'NguoiBan', 1),
('NB003', N'Phạm Mỹ Linh', '0911000003', 'mylinh.cake@gmail.com', '123456', N'NguoiBan', 1),
('NB004', N'Hoàng Nam Anh', '0911000004', 'namanh.sweets@gmail.com', '123456', N'NguoiBan', 1),
('NB005', N'Đỗ Thùy Chi', '0911000005', 'thuychi.dessert@gmail.com', '123456', N'NguoiBan', 1),
('KH001', N'Đặng Hoàng Yến', '0922000001', 'hoangyen@gmail.com', '123456', N'NguoiMua', 1),
('KH002', N'Bùi Tiến Dũng', '0922000002', 'tiendung@gmail.com', '123456', N'NguoiMua', 1),
('KH003', N'Vũ Phương Thảo', '0922000003', 'phuongthao@gmail.com', '123456', N'NguoiMua', 1);
GO

-- 3. Insert Cửa hàng (Phải có Cửa hàng trước khi có Sản phẩm)
INSERT INTO dbo.CuaHang (MaCuaHang, TenCuaHang, MaND, TrangThaiCH) VALUES
('CH001', N'Tiệm Bánh Tiramisu Xinh', 'NB001', 1),
('CH002', N'Tiệm Bánh Kem Hạnh Phúc', 'NB002', 1),
('CH003', N'Linh Cake & Tea', 'NB003', 1),
('CH004', N'Sweets Home - Bánh Su Kem', 'NB004', 1),
('CH005', N'Chi Dessert - Bánh Quy Thủ Công', 'NB005', 1);
GO

-- 4. Insert Sản phẩm
INSERT INTO dbo.SanPham (MaSanPham, Ten, MaDanhMuc, MaCuaHang, GiaBan, DonViTinh, TrangThai, HinhAnh, MoTa, SoLuongTon) VALUES
('SP001', N'Bánh Mì Pháp', 'DMC001', 'CH001', 25000, N'cái', N'Đang bán', 'banh-mi-phap.jpg', N'Bánh mì truyền thống Pháp, giòn thơm', 50),
('SP002', N'Bánh Ngọt Sô-Cô-La', 'DMC002', 'CH001', 30000, N'cái', N'Đang bán', 'banh-ngot-socola.jpg', N'Bánh ngọt phủ sô-cô-la, mềm mịn', 30),
('SP003', N'Bánh Sinh Nhật Kem Dâu', 'DMC003', 'CH001', 350000, N'cái', N'Đang bán', 'banh-kem-dau.jpg', N'Bánh sinh nhật kem dâu tươi', 10),
('SP004', N'Bánh Tiramisu Truyền Thống', 'DMC002', 'CH001', 45000, N'cái', N'Đang bán', 'banh-tiramisu.jpg', N'Bánh Tiramisu hương vị Ý đậm đà', 0),
('SP005', N'Bánh Kem Bắp', 'DMC003', 'CH002', 280000, N'cái', N'Đang bán', 'banh-kem-bap.jpg', N'Bánh kem bắp thơm lừng, ít ngọt', 5),
('SP006', N'Bánh Mì Hoa Cúc', 'DMC001', 'CH002', 85000, N'cái', N'Đang bán', 'banh-mi-hoa-cuc.jpg', N'Bánh mì hoa cúc nhập khẩu Pháp', 20),
('SP007', N'Bánh Su Kem Thiên Nga', 'DMC002', 'CH002', 15000, N'cái', N'Đang bán', 'banh-su-kem.jpg', N'Bánh su kem tạo hình thiên nga đẹp mắt', 100),
('SP008', N'Bánh Cupcake Trái Cây', 'DMC008', 'CH002', 35000, N'cái', N'Đang bán', 'banh-cupcake.jpg', N'Cupcake trang trí trái cây tươi', 15),
('SP009', N'Bánh Croissant', 'DMC009', 'CH003', 45000, N'cái', N'Đang bán', 'banh-croissaint.jpg', N'Bánh croissant kiểu Pháp, giòn xốp', 40),
('SP010', N'Bánh Mousse Socola', 'DMC002', 'CH003', 55000, N'cái', N'Đang bán', 'banh-mousse-socola.jpg', N'Bánh mousse socola mềm mịn', 25),
('SP011', N'Bánh Tart Trái Cây', 'DMC008', 'CH003', 60000, N'cái', N'Đang bán', 'banh-tart-trai-cay.jpg', N'Tart giòn, phủ trái cây tươi', 12),
('SP012', N'Bánh Cheesecake Chanh Dây', 'DMC003', 'CH003', 120000, N'cái', N'Đang bán', 'cheesecake-chanh-day.jpg', N'Cheesecake béo ngậy vị chanh dây', 0),
('SP013', N'Bánh Bông Lan Truyền Thống', 'DMC001', 'CH004', 25000, N'cái', N'Đang bán', 'banh-bong-lan.jpg', N'Bánh bông lan mềm, thơm mùi vani', 60),
('SP014', N'Bánh Donut Socola', 'DMC002', 'CH004', 20000, N'cái', N'Đang bán', 'banh-donut-socola.jpg', N'Donut phủ socola giòn', 45),
('SP015', N'Bánh Macaron Pháp', 'DMC008', 'CH004', 30000, N'cái', N'Đang bán', 'banh-macaron.jpg', N'Macaron đủ màu sắc, ngọt ngào', 200),
('SP016', N'Bánh Baguette Nguyên Cám', 'DMC001', 'CH004', 30000, N'cái', N'Đang bán', 'banh-baguette-nguyen-cam.jpg', N'Bánh baguette tốt cho sức khỏe', 35),
('SP017', N'Bánh Eclair Vanilla', 'DMC002', 'CH005', 40000, N'cái', N'Đang bán', 'banh-eclair-vanilla.jpg', N'Bánh Eclair nhân kem vanilla béo', 18),
('SP018', N'Bánh Pudding Socola', 'DMC002', 'CH005', 35000, N'cái', N'Đang bán', 'banh-pudding-socola.jpg', N'Pudding socola mềm mịn', 22),
('SP019', N'Bánh Pie Táo', 'DMC008', 'CH005', 45000, N'cái', N'Đang bán', 'banh-pie-tao.jpg', N'Bánh pie nhân táo thơm quế', 0),
('SP020', N'Bánh Muffin Việt Quất', 'DMC008', 'CH005', 25000, N'cái', N'Đang bán', 'banh-muffin-viet-quat.jpg', N'Muffin việt quất chua ngọt', 50);
GO

-- 5. Insert Đơn hàng
INSERT INTO dbo.DonHang (MaDon, MaKhachHang, NgayDat, TrangThai, TongTien, GhiChu) VALUES
('DH001', 'KH001', '2023-09-22 08:00:00', N'Đã thanh toán', 350000, N'Giao nhanh'),
('DH002', 'KH002', '2023-09-21 09:30:00', N'Chờ xử lý', 500000, N'Khách dặn gọi trước khi giao');
GO

-- 6. Insert Chi tiết đơn hàng
INSERT INTO dbo.ChiTietDonHang (MaDon, MaSanPham, SoLuong, DonGia, GiamGia) VALUES
('DH001', 'SP003', 1, 350000, 0),
('DH002', 'SP001', 10, 25000, 0),
('DH002', 'SP002', 5, 50000, 0);
GO

---------------------------------------------------------
-- KIỂM TRA LẠI DỮ LIỆU
---------------------------------------------------------
SELECT * FROM SanPham;
SELECT * FROM DonHang;
SELECT * FROM ChiTietDonHang;
GO