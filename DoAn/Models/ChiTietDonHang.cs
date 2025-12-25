using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DoAn.Models
{
    public class ChiTietDonHang
    {
        public string MaDon { get; set; }
        public string MaSanPham { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal GiamGia { get; set; }
        public decimal ThanhTien { get; set; }

        // Thuộc tính bổ sung để hiển thị tên bánh trên giao diện quản lý
        public string TenSanPham { get; set; }
    }
}