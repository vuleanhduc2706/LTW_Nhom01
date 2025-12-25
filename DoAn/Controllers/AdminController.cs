using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Configuration;
using System.Web.Mvc;
using DoAn.Models;

namespace DoAn.Controllers
{
    [KiemTraKhoaTaiKhoan]
    public class AdminController : Controller
    {
        string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;

        // Trang danh sách người dùng
        public ActionResult QuanLyNguoiDung()
        {
            // Kiểm tra quyền Admin
            var user = (NguoiDung)Session["TaiKhoan"];
            if (user == null || user.QuyenHan.Trim() != "Admin") return RedirectToAction("Index", "Home");

            List<NguoiDung> ds = new List<NguoiDung>();
            using (SqlConnection conn = new SqlConnection(constr))
            {
                string sql = "SELECT * FROM NguoiDung WHERE QuyenHan <> 'Admin'";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    ds.Add(new NguoiDung
                    {
                        MaND = rdr["MaND"].ToString(),
                        HoTen = rdr["HoTen"].ToString(),
                        SDT = rdr["SDT"].ToString(),
                        Email = rdr["Email"].ToString(),
                        QuyenHan = rdr["QuyenHan"].ToString(),
                        TrangThai = Convert.ToInt32(rdr["TrangThai"])
                    });
                }
            }
            return View(ds);
        }

        // =================================================================================
        // 1. HÀM KHÓA/MỞ TÀI KHOẢN (Đã thêm chức năng xóa dữ liệu khi khóa)
        // =================================================================================
        [HttpPost]
        public ActionResult DoiTrangThai(string maND, int status)
        {
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // BƯỚC 1: Kiểm tra xem ông này có phải Người Bán không?
                    // (Lấy trực tiếp từ DB cho chắc ăn, không đoán mò qua chữ "NB")
                    string checkRoleSql = "SELECT QuyenHan FROM NguoiDung WHERE MaND = @id";
                    SqlCommand cmdRole = new SqlCommand(checkRoleSql, conn, trans);
                    cmdRole.Parameters.AddWithValue("@id", maND);
                    string quyenHan = cmdRole.ExecuteScalar()?.ToString().Trim();

                    // BƯỚC 2: Xử lý Sản Phẩm nếu là Người Bán
                    if (quyenHan == "NguoiBan")
                    {
                        if (status == 0) // --- LỆNH KHÓA ---
                        {
                            // Thay vì XÓA, ta ẨN toàn bộ sản phẩm của shop này
                            string sqlHidePro = @"UPDATE SanPham 
                                                  SET TrangThai = N'Bị gỡ bởi Admin' 
                                                  WHERE MaCuaHang IN (SELECT MaCuaHang FROM CuaHang WHERE MaND = @id)";
                            SqlCommand cmdHide = new SqlCommand(sqlHidePro, conn, trans);
                            cmdHide.Parameters.AddWithValue("@id", maND);
                            cmdHide.ExecuteNonQuery();
                        }
                        else // --- LỆNH MỞ KHÓA ---
                        {
                            // Khôi phục trạng thái sản phẩm thông minh
                            // (Nếu còn hàng thì cho bán, hết hàng thì để ngừng bán)
                            string sqlRestorePro = @"UPDATE SanPham 
                                                     SET TrangThai = CASE 
                                                         WHEN SoLuongTon > 0 THEN N'Đang bán' 
                                                         ELSE N'Ngừng bán' 
                                                     END
                                                     WHERE MaCuaHang IN (SELECT MaCuaHang FROM CuaHang WHERE MaND = @id)";
                            SqlCommand cmdRestore = new SqlCommand(sqlRestorePro, conn, trans);
                            cmdRestore.Parameters.AddWithValue("@id", maND);
                            cmdRestore.ExecuteNonQuery();
                        }
                    }

                    // BƯỚC 3: Cập nhật trạng thái Tài khoản (NguoiDung)
                    string sqlUser = "UPDATE NguoiDung SET TrangThai = @st WHERE MaND = @id";
                    SqlCommand cmdUser = new SqlCommand(sqlUser, conn, trans);
                    cmdUser.Parameters.AddWithValue("@st", status);
                    cmdUser.Parameters.AddWithValue("@id", maND);
                    cmdUser.ExecuteNonQuery();

                    // Nếu khóa, ta xóa luôn Session của người đó (để họ bị đá ra ngay lập tức nếu đang online)
                    // Lưu ý: Cách này chỉ xóa được session nếu chạy trên cùng máy server, 
                    // nhưng logic [KiemTraKhoaTaiKhoan] ở các controller kia đã lo việc này rồi.

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    // Ghi lại lỗi vào TempData để hiện ra màn hình (nếu muốn debug)
                    TempData["Error"] = "Lỗi khi cập nhật trạng thái: " + ex.Message;
                }
            }
            return RedirectToAction("QuanLyNguoiDung");
        }

        // Trang hiển thị danh sách chờ duyệt
        public ActionResult DuyetNguoiBan()
        {
            List<NguoiDung> ds = new List<NguoiDung>();
            using (SqlConnection conn = new SqlConnection(constr))
            {
                string sql = "SELECT * FROM NguoiDung WHERE QuyenHan = 'NguoiBan' AND TrangThai = 2";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    ds.Add(new NguoiDung
                    {
                        MaND = rdr["MaND"].ToString(),
                        HoTen = rdr["HoTen"].ToString(),
                        Email = rdr["Email"].ToString(),
                        SDT = rdr["SDT"].ToString(),
                        QuyenHan = rdr["QuyenHan"].ToString()
                    });
                }
            }
            return View(ds);
        }


        // =================================================================================
        // 2. HÀM DUYỆT NGƯỜI BÁN (Đã thêm kiểm tra trùng lặp để tránh lỗi)
        // =================================================================================
        [HttpPost]
        public ActionResult PheDuyet(string maND)
        {
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // B1: Kích hoạt tài khoản
                    string sqlUser = "UPDATE NguoiDung SET TrangThai = 1 WHERE MaND = @id";
                    SqlCommand cmd1 = new SqlCommand(sqlUser, conn, trans);
                    cmd1.Parameters.AddWithValue("@id", maND);
                    cmd1.ExecuteNonQuery();

                    // B2: Tạo Shop (Có kiểm tra xem đã tồn tại chưa)
                    string maCH = "CH" + maND.Substring(2);

                    // Check xem shop này có chưa?
                    SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM CuaHang WHERE MaCuaHang = @mch", conn, trans);
                    checkCmd.Parameters.AddWithValue("@mch", maCH);
                    int count = (int)checkCmd.ExecuteScalar();

                    if (count == 0) // Chưa có mới thêm
                    {
                        string sqlStore = "INSERT INTO CuaHang (MaCuaHang, MaND, TenCuaHang) VALUES (@maCH, @id, @tenCH)";
                        SqlCommand cmd2 = new SqlCommand(sqlStore, conn, trans);
                        cmd2.Parameters.AddWithValue("@maCH", maCH);
                        cmd2.Parameters.AddWithValue("@id", maND);
                        cmd2.Parameters.AddWithValue("@tenCH", "Tiệm Bánh Mới");
                        cmd2.ExecuteNonQuery();
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                }
            }
            // Quay lại trang Quản lý người dùng cho tiện
            return RedirectToAction("QuanLyNguoiDung");
        }

        public string TaoMaNguoiDung(string quyenHan)
        {
            string prefix = (quyenHan == "NguoiMua") ? "KH" : "NB";
            string moiMa = prefix + "001";

            using (SqlConnection con = new SqlConnection(constr))
            {
                string sql = "SELECT TOP 1 MaND FROM NguoiDung WHERE MaND LIKE @prefix + '%' ORDER BY MaND DESC";
                SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@prefix", prefix);
                con.Open();

                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    string maHienTai = result.ToString();
                    int soThuTu = int.Parse(maHienTai.Substring(2)) + 1;
                    moiMa = prefix + soThuTu.ToString("D3");
                }
            }
            return moiMa;
        }

        // 1. Xem danh sách toàn bộ đơn hàng của hệ thống
        public ActionResult QuanLyDonHang()
        {
            // Kiểm tra quyền Admin
            var user = (NguoiDung)Session["TaiKhoan"];
            if (user == null || user.QuyenHan.Trim() != "Admin") return RedirectToAction("Index", "Home");

            List<DonHang> ds = new List<DonHang>();
            using (SqlConnection conn = new SqlConnection(constr))
            {
                // Join thêm bảng CuaHang để biết đơn này của Shop nào
                string sql = @"
            SELECT dh.MaDon, dh.NgayDat, dh.TongTien, dh.TrangThai, 
                   nd.HoTen AS TenKhach, ch.TenCuaHang
            FROM DonHang dh
            JOIN NguoiDung nd ON dh.MaKhachHang = nd.MaND
            JOIN ChiTietDonHang ct ON dh.MaDon = ct.MaDon
            JOIN SanPham sp ON ct.MaSanPham = sp.MaSanPham
            JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang
            GROUP BY dh.MaDon, dh.NgayDat, dh.TongTien, dh.TrangThai, nd.HoTen, ch.TenCuaHang
            ORDER BY dh.NgayDat DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    ds.Add(new DonHang
                    {
                        MaDon = rdr["MaDon"].ToString(),
                        NgayDat = Convert.ToDateTime(rdr["NgayDat"]),
                        TongTien = rdr["TongTien"] != DBNull.Value ? Convert.ToDecimal(rdr["TongTien"]) : 0,
                        TrangThai = rdr["TrangThai"].ToString(),
                        // Tận dụng thuộc tính GhiChu để lưu Tên Cửa Hàng tạm thời cho view hiển thị
                        GhiChu = rdr["TenCuaHang"].ToString(),
                        KhachHang = new NguoiDung { HoTen = rdr["TenKhach"].ToString() }
                    });
                }
            }
            return View(ds);
        }

        // 2. Hàm Xóa đơn hàng (Thực chất là Hủy và thông báo)
        [HttpPost]
        public ActionResult XoaDonHangByAdmin(string maDon)
        {
            // Kiểm tra quyền Admin
            var user = (NguoiDung)Session["TaiKhoan"];
            if (user == null || user.QuyenHan.Trim() != "Admin") return RedirectToAction("DangNhap", "Home");

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // B1: Lấy trạng thái hiện tại để kiểm tra
                    string sqlCheck = "SELECT TrangThai FROM DonHang WHERE MaDon = @id";
                    SqlCommand cmdCheck = new SqlCommand(sqlCheck, conn, trans);
                    cmdCheck.Parameters.AddWithValue("@id", maDon);
                    string trangThaiCu = cmdCheck.ExecuteScalar()?.ToString();

                    // B2: Nếu đơn chưa hủy thì phải Hoàn trả số lượng tồn kho
                    if (trangThaiCu != "Đã hủy" && trangThaiCu != "Bị hủy bởi Admin")
                    {
                        string sqlRefund = @"UPDATE SanPham 
                                     SET SoLuongTon = SoLuongTon + ct.SoLuong 
                                     FROM SanPham sp 
                                     JOIN ChiTietDonHang ct ON sp.MaSanPham = ct.MaSanPham 
                                     WHERE ct.MaDon = @id";
                        SqlCommand cmdRefund = new SqlCommand(sqlRefund, conn, trans);
                        cmdRefund.Parameters.AddWithValue("@id", maDon);
                        cmdRefund.ExecuteNonQuery();
                    }

                    // B3: Cập nhật trạng thái và Ghi chú thông báo
                    string thongBao = "Đơn hàng đã bị xóa bởi Admin. Vui lòng liên hệ Admin để được giải quyết.";
                    string sqlUpd = @"UPDATE DonHang 
                              SET TrangThai = N'Bị hủy bởi Admin', 
                                  GhiChu = @msg 
                              WHERE MaDon = @id";

                    SqlCommand cmdUpd = new SqlCommand(sqlUpd, conn, trans);
                    cmdUpd.Parameters.AddWithValue("@id", maDon);
                    cmdUpd.Parameters.AddWithValue("@msg", thongBao);
                    cmdUpd.ExecuteNonQuery();

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                }
            }
            return RedirectToAction("QuanLyDonHang");
        }

        // 1. Xem danh sách toàn bộ sản phẩm của hệ thống
        public ActionResult QuanLySanPham()
        {
            var user = (NguoiDung)Session["TaiKhoan"];
            if (user == null || user.QuyenHan.Trim() != "Admin") return RedirectToAction("Index", "Home");

            List<SanPham> ds = new List<SanPham>();
            using (SqlConnection conn = new SqlConnection(constr))
            {
                // KHÔNG thêm điều kiện ẩn, Admin phải thấy hết
                string sql = @"
                    SELECT sp.MaSanPham, sp.Ten, sp.HinhAnh, sp.GiaBan, sp.TrangThai, sp.SoLuongTon,
                           ch.TenCuaHang, dm.Ten AS TenDanhMuc
                    FROM SanPham sp
                    JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang
                    JOIN DanhMuc dm ON sp.MaDanhMuc = dm.MaDanhMuc
                    ORDER BY sp.MaSanPham DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    ds.Add(new SanPham
                    {
                        MaSanPham = rdr["MaSanPham"].ToString(),
                        Ten = rdr["Ten"].ToString(),
                        HinhAnh = rdr["HinhAnh"].ToString(),
                        GiaBan = Convert.ToDecimal(rdr["GiaBan"]),
                        TrangThai = rdr["TrangThai"].ToString(),
                        SoLuongTon = Convert.ToInt32(rdr["SoLuongTon"]),
                        MoTa = rdr["TenCuaHang"].ToString(), // Mượn tạm biến MoTa để lưu tên Shop
                        TenDanhMuc = rdr["TenDanhMuc"].ToString()
                    });
                }
            }
            return View(ds);
        }

        // 2. GỠ SẢN PHẨM (Ẩn đi)
        [HttpPost]
        public ActionResult GoSanPhamByAdmin(string id)
        {
            var user = (NguoiDung)Session["TaiKhoan"];
            if (user == null || user.QuyenHan.Trim() != "Admin") return RedirectToAction("DangNhap", "Home");

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                // Đổi trạng thái -> Code bên User sẽ tự lọc bỏ dòng này
                string sql = "UPDATE SanPham SET TrangThai = N'Bị gỡ bởi Admin' WHERE MaSanPham = @id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("QuanLySanPham");
        }

        // 3. KHÔI PHỤC SẢN PHẨM (Hiện lại)
        [HttpPost]
        public ActionResult KhoiPhucSanPham(string id)
        {
            var user = (NguoiDung)Session["TaiKhoan"];
            if (user == null || user.QuyenHan.Trim() != "Admin") return RedirectToAction("DangNhap", "Home");

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                // Logic thông minh: Còn hàng -> Đang bán, Hết hàng -> Ngừng bán
                string sql = @"UPDATE SanPham 
                               SET TrangThai = CASE 
                                    WHEN SoLuongTon > 0 THEN N'Đang bán' 
                                    ELSE N'Ngừng bán' 
                               END
                               WHERE MaSanPham = @id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("QuanLySanPham");
        }
    }
}