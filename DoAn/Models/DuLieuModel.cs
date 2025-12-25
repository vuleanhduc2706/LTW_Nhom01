using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DoAn.Models
{
    public class DuLieuModel
    {
        public SanPham Product { get; set; }
        public List<SanPham> Products { get; set; }
        public DuLieuModel()
        {
            Product = new SanPham();
            Products = new List<SanPham>();
        }
    }
}