using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Configuration;

namespace DoAn.Models
{
    public class ItemGioHang
    {
        public string MaSanPham { get; set; }
        public string TenSanPham { get; set; }
        public string HinhAnh { get; set; }
        public decimal DonGia { get; set; }
        public int SoLuong { get; set; }
        public decimal ThanhTien { get { return SoLuong * DonGia; } }

        // Hàm khởi tạo để lấy thông tin từ DB dựa vào MaSP
        public ItemGioHang(string maSP)
        {
            this.MaSanPham = maSP;
            string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                string sql = "SELECT Ten, HinhAnh, GiaBan FROM SanPham WHERE MaSanPham = @maSP";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@maSP", maSP);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        this.TenSanPham = reader["Ten"].ToString();
                        this.HinhAnh = reader["HinhAnh"].ToString();
                        this.DonGia = (decimal)reader["GiaBan"];
                        this.SoLuong = 1; // Mặc định mua 1
                    }
                }
            }
        }
        public ItemGioHang() { }
    }
}