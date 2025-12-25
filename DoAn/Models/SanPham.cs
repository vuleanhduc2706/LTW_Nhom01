using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DoAn.Models
{
    public class SanPham
    {
        public string MaSanPham { get; set; }
        public string MaDanhMuc { get; set; }
        public string Ten { get; set; }
        public decimal GiaBan { get; set; }
        public string DonViTinh { get; set; }
        public string TrangThai { get; set; }
        public string MoTa { get; set; }
        public string HinhAnh { get; set; }
        public string TenDanhMuc { get; set; }
        public int SoLuongTon { get; set; }
    }
}