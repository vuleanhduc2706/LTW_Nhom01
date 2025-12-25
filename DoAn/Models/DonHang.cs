using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

    namespace DoAn.Models
    {
        public class DonHang
        {
            public string MaDon { get; set; }
            public string MaKhachHang { get; set; }
            public DateTime NgayDat { get; set; }
            public string TrangThai { get; set; }
            public decimal TongTien { get; set; }
            public string GhiChu { get; set; }

            // Các thuộc tính bổ sung để chứa dữ liệu từ lệnh JOIN
            public NguoiDung KhachHang { get; set; }
            public string TenSanPham { get; set; }
            public int SoLuong { get; set; }

            // --- THÊM 2 DÒNG NÀY ---
            public decimal DonGia { get; set; }
            public decimal ThanhTien { get; set; }

            public string TenNguoiNhan { get; set; }
            public string SDTNguoiNhan { get; set; }
            public string DiaChiGiaoHang { get; set; }
        }
    }