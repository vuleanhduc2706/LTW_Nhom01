using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DoAn.Models
{
    public class DuLieu
    {
        public List<SanPham> DanhSachSanPham { get; set; }
        public List<DanhMuc> DanhSachDanhMuc { get; set; }

        public DuLieu()
        {
            DanhSachSanPham = new List<SanPham>();
            DanhSachDanhMuc = new List<DanhMuc>();
        }
    }
}