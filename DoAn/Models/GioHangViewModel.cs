using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DoAn.Models
{
    public class GioHangViewModel
    {
        public List<ItemGioHang> GioHangHienTai { get; set; }
        public List<DonHang> DonHangDaDat { get; set; }
    }
}