using DoAn.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Configuration;
using System.Web.Mvc;

namespace DoAn.Controllers
{
    [KiemTraKhoaTaiKhoan]
    public class GioHangController : Controller
    {
        private readonly string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;

        // 1. Lấy giỏ hàng từ Session
        public List<ItemGioHang> LayGioHang()
        {
            List<ItemGioHang> lstGioHang = Session["GioHang"] as List<ItemGioHang>;
            if (lstGioHang == null)
            {
                lstGioHang = new List<ItemGioHang>();
                Session["GioHang"] = lstGioHang;
            }
            return lstGioHang;
        }

        // 2. Thêm vào giỏ hàng
        public ActionResult ThemGioHang(string ms, string strURL)
        {
            var user = Session["TaiKhoan"] as DoAn.Models.NguoiDung;
            if (user != null && (user.QuyenHan.Trim() == "NguoiBan" || user.QuyenHan.Trim() == "Admin"))
            {
                return RedirectToAction("Index", "Home");
            }

            List<ItemGioHang> lstGioHang = LayGioHang();
            ItemGioHang sp = lstGioHang.Find(n => n.MaSanPham == ms);
            if (sp == null)
            {
                sp = new ItemGioHang(ms); // Đảm bảo constructor class ItemGioHang của bạn xử lý việc lấy thông tin SP
                lstGioHang.Add(sp);
            }
            else
            {
                sp.SoLuong++;
            }
            return RedirectToAction("GioHang", "GioHang");
        }

        // 3. Hiển thị giỏ hàng (ĐÃ SỬA LỖI TẠI ĐÂY)
        public ActionResult GioHang()
        {
            List<ItemGioHang> lstGioHang = LayGioHang();

            // Tính toán tổng tiền để hiển thị (nếu View cần)
            if (lstGioHang.Count > 0)
            {
                ViewBag.TongSoLuong = lstGioHang.Sum(n => n.SoLuong);
                ViewBag.TongTien = lstGioHang.Sum(n => n.ThanhTien);
            }
            else
            {
                ViewBag.TongSoLuong = 0;
                ViewBag.TongTien = 0;
            }

            // QUAN TRỌNG: Trả về đúng kiểu List<ItemGioHang> khớp với @model của View
            return View(lstGioHang);
        }

        // 4. Cập nhật số lượng
        public ActionResult CapNhatGioHang(string maSP, FormCollection f)
        {
            List<ItemGioHang> lstGioHang = LayGioHang();
            ItemGioHang sp = lstGioHang.SingleOrDefault(n => n.MaSanPham == maSP);
            if (sp != null)
            {
                // Kiểm tra số lượng nhập vào hợp lệ
                int soLuongMoi;
                if (int.TryParse(f["sl"] ?? f["txtSoLuong"], out soLuongMoi) && soLuongMoi > 0)
                {
                    sp.SoLuong = soLuongMoi;
                }
            }
            return RedirectToAction("GioHang");
        }

        // 5. Xóa giỏ hàng
        public ActionResult XoaGioHang(string maSP)
        {
            List<ItemGioHang> lstGioHang = LayGioHang();
            lstGioHang.RemoveAll(n => n.MaSanPham == maSP);

            if (lstGioHang.Count == 0) return RedirectToAction("Index", "Home");
            return RedirectToAction("GioHang");
        }

        // 6. Trang xác nhận đặt hàng (GET)
        public ActionResult DatHang()
        {
            List<ItemGioHang> lstGioHang = LayGioHang();
            if (lstGioHang == null || lstGioHang.Count == 0) return RedirectToAction("Index", "Home");

            ViewBag.TongTien = lstGioHang.Sum(n => n.ThanhTien);
            return View(lstGioHang); // Trả về List cho View check out
        }

        // 7. XỬ LÝ ĐẶT HÀNG (POST)
        [HttpPost]
        public ActionResult DatHang(FormCollection f)
        {
            // A. Kiểm tra đăng nhập
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (DoAn.Models.NguoiDung)Session["TaiKhoan"];

            // B. Kiểm tra giỏ hàng
            List<ItemGioHang> lstGioHang = LayGioHang();
            if (lstGioHang == null || lstGioHang.Count == 0) return RedirectToAction("Index", "Home");

            string ghiChu = f["GhiChu"];

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();
                try
                {
                    // C. Kiểm tra tồn kho & Trừ kho
                    foreach (var item in lstGioHang)
                    {
                        string sqlCheck = "SELECT SoLuongTon FROM SanPham WHERE MaSanPham = @maSP";
                        SqlCommand cmdCheck = new SqlCommand(sqlCheck, conn, transaction);
                        cmdCheck.Parameters.AddWithValue("@maSP", item.MaSanPham);

                        object result = cmdCheck.ExecuteScalar();
                        int tonKho = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;

                        if (tonKho < item.SoLuong)
                        {
                            transaction.Rollback();
                            TempData["Message"] = $"Sản phẩm '{item.TenSanPham}' chỉ còn {tonKho} cái. Vui lòng cập nhật lại!";
                            return RedirectToAction("GioHang");
                        }

                        // Trừ kho
                        string sqlUpdate = "UPDATE SanPham SET SoLuongTon = SoLuongTon - @sl WHERE MaSanPham = @maSP";
                        SqlCommand cmdUpdate = new SqlCommand(sqlUpdate, conn, transaction);
                        cmdUpdate.Parameters.AddWithValue("@sl", item.SoLuong);
                        cmdUpdate.Parameters.AddWithValue("@maSP", item.MaSanPham);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    // D. Tạo đơn hàng (Lưu địa chỉ từ Session)
                    string maDon = "DH" + DateTime.Now.ToString("ddHHmmss");
                    string sqlDon = @"INSERT INTO DonHang(MaDon, MaKhachHang, NgayDat, TrangThai, TongTien, GhiChu, TenNguoiNhan, SDTNguoiNhan, DiaChiGiaoHang) 
                                      VALUES(@ma, @makh, GETDATE(), N'Chờ xử lý', @tong, @ghichu, @ten, @sdt, @diachi)";

                    SqlCommand cmdDon = new SqlCommand(sqlDon, conn, transaction);
                    cmdDon.Parameters.AddWithValue("@ma", maDon);
                    cmdDon.Parameters.AddWithValue("@makh", user.MaND);
                    cmdDon.Parameters.AddWithValue("@tong", lstGioHang.Sum(x => x.ThanhTien));
                    cmdDon.Parameters.AddWithValue("@ghichu", (object)ghiChu ?? DBNull.Value);

                    // LẤY THÔNG TIN TỪ USER SESSION ĐỂ LƯU
                    cmdDon.Parameters.AddWithValue("@ten", user.HoTen);
                    cmdDon.Parameters.AddWithValue("@sdt", user.SDT);
                    cmdDon.Parameters.AddWithValue("@diachi", user.DiaChi);

                    cmdDon.ExecuteNonQuery();

                    // E. Lưu chi tiết
                    foreach (var item in lstGioHang)
                    {
                        string sqlCT = "INSERT INTO ChiTietDonHang(MaDon, MaSanPham, SoLuong, DonGia, GiamGia) VALUES(@ma, @sp, @sl, @gia, 0)";
                        SqlCommand cmdCT = new SqlCommand(sqlCT, conn, transaction);
                        cmdCT.Parameters.AddWithValue("@ma", maDon);
                        cmdCT.Parameters.AddWithValue("@sp", item.MaSanPham);
                        cmdCT.Parameters.AddWithValue("@sl", item.SoLuong);
                        cmdCT.Parameters.AddWithValue("@gia", item.DonGia);
                        cmdCT.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    Session["GioHang"] = null; // Xóa giỏ hàng
                    TempData["Success"] = "Đặt hàng thành công!";
                    return RedirectToAction("LichSuMuaHang");
                }
                catch (Exception ex)
                {
                    if (transaction != null) transaction.Rollback();
                    TempData["Message"] = "Lỗi hệ thống: " + ex.Message;
                    return RedirectToAction("GioHang");
                }
            }
        }

        // 8. Lịch sử mua hàng (Nên tách ra View riêng nếu cần hiển thị)
        public ActionResult LichSuMuaHang()
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (NguoiDung)Session["TaiKhoan"];

            List<DonHang> ds = new List<DonHang>();
            using (SqlConnection con = new SqlConnection(constr))
            {
                string sql = @"SELECT dh.MaDon, dh.NgayDat, dh.TrangThai, 
                                      sp.Ten as TenSanPham, 
                                      ct.SoLuong, ct.DonGia, 
                                      (ct.SoLuong * ct.DonGia) as ThanhTien 
                               FROM DonHang dh 
                               INNER JOIN ChiTietDonHang ct ON dh.MaDon = ct.MaDon
                               INNER JOIN SanPham sp ON ct.MaSanPham = sp.MaSanPham
                               WHERE dh.MaKhachHang = @maKH 
                               ORDER BY dh.NgayDat DESC";

                SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@maKH", user.MaND);
                con.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    ds.Add(new DonHang
                    {
                        MaDon = rdr["MaDon"].ToString(),
                        NgayDat = Convert.ToDateTime(rdr["NgayDat"]),
                        TenSanPham = rdr["TenSanPham"].ToString(),
                        SoLuong = Convert.ToInt32(rdr["SoLuong"]),
                        DonGia = Convert.ToDecimal(rdr["DonGia"]),
                        ThanhTien = Convert.ToDecimal(rdr["ThanhTien"]),
                        TrangThai = rdr["TrangThai"].ToString()
                    });
                }
            }
            // Bạn cần tạo View LichSuMuaHang.cshtml riêng nhận model là List<DonHang>
            return View(ds);
        }

        // 9. Hủy đơn hàng
        [HttpPost]
        public ActionResult HuyDonHang(string maDon)
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (NguoiDung)Session["TaiKhoan"];

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // Kiểm tra đơn hàng có đúng của user này và đang 'Chờ xử lý' không
                    string sqlCheck = "SELECT TrangThai FROM DonHang WHERE MaDon = @maDon AND MaKhachHang = @maKH";
                    SqlCommand cmdCheck = new SqlCommand(sqlCheck, conn, trans);
                    cmdCheck.Parameters.AddWithValue("@maDon", maDon);
                    cmdCheck.Parameters.AddWithValue("@maKH", user.MaND);

                    string trangThai = cmdCheck.ExecuteScalar()?.ToString();

                    if (trangThai == "Chờ xử lý")
                    {
                        // Lấy danh sách sản phẩm để hoàn kho
                        string sqlGetItems = "SELECT MaSanPham, SoLuong FROM ChiTietDonHang WHERE MaDon = @maDon";
                        SqlCommand cmdItems = new SqlCommand(sqlGetItems, conn, trans);
                        cmdItems.Parameters.AddWithValue("@maDon", maDon);

                        List<KeyValuePair<string, int>> items = new List<KeyValuePair<string, int>>();
                        using (SqlDataReader rdr = cmdItems.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                items.Add(new KeyValuePair<string, int>(rdr["MaSanPham"].ToString(), Convert.ToInt32(rdr["SoLuong"])));
                            }
                        }

                        // Hoàn kho
                        foreach (var item in items)
                        {
                            string sqlRestore = "UPDATE SanPham SET SoLuongTon = SoLuongTon + @sl WHERE MaSanPham = @maSP";
                            SqlCommand cmdRestore = new SqlCommand(sqlRestore, conn, trans);
                            cmdRestore.Parameters.AddWithValue("@sl", item.Value);
                            cmdRestore.Parameters.AddWithValue("@maSP", item.Key);
                            cmdRestore.ExecuteNonQuery();
                        }

                        // Cập nhật trạng thái
                        string sqlCancel = "UPDATE DonHang SET TrangThai = N'Đã hủy' WHERE MaDon = @maDon";
                        SqlCommand cmdCancel = new SqlCommand(sqlCancel, conn, trans);
                        cmdCancel.Parameters.AddWithValue("@maDon", maDon);
                        cmdCancel.ExecuteNonQuery();

                        trans.Commit();
                        TempData["Message"] = "Đã hủy đơn hàng thành công.";
                    }
                    else
                    {
                        TempData["Message"] = "Không thể hủy đơn hàng này.";
                        trans.Rollback();
                    }
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    TempData["Message"] = "Lỗi khi hủy đơn: " + ex.Message;
                }
            }
            // Nếu bạn muốn quay lại trang lịch sử thì đổi thành RedirectToAction("LichSuMuaHang")
            return RedirectToAction("GioHang");
        }
    }
}