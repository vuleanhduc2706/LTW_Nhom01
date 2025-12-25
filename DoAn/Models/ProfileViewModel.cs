using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DoAn.Models
{
    public class ProfileViewModel
    {
        // --- THÔNG TIN CHUNG (Dùng cho cả Khách & Chủ Shop) ---
        public string MaND { get; set; }

        [Display(Name = "Họ và tên")]
        public string HoTen { get; set; }

        [Display(Name = "Số điện thoại")]
        public string SDT { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Địa chỉ cá nhân / Giao hàng")]
        public string DiaChi { get; set; } // <--- THÊM MỚI

        public string QuyenHan { get; set; }

        // --- THÔNG TIN RIÊNG CHO NGƯỜI BÁN ---
        [Display(Name = "Tên Cửa Hàng")]
        public string TenCuaHang { get; set; }

        [Display(Name = "Địa chỉ Cửa Hàng")]
        public string DiaChiCuaHang { get; set; }
    }
}