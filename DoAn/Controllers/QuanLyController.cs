using DoAn.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace DoAn.Controllers
{
    [KiemTraKhoaTaiKhoan]
    public class QuanLyController : Controller
    {
        private readonly string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;

        // 1. Hiện sản phẩm của Shop đó
        public ActionResult KhoHang() // Bỏ tham số string maND ở đây để lấy từ Session
        {
            // 1. Kiểm tra đăng nhập và lấy maND từ Session
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (NguoiDung)Session["TaiKhoan"];
            string maND = user.MaND;

            List<SanPham> ds = new List<SanPham>();
            using (SqlConnection con = new SqlConnection(constr))
            {
                // THÊM ĐIỀU KIỆN: AND sp.TrangThai <> N'Bị gỡ bởi Admin'
                // Điều này đảm bảo sản phẩm bị gỡ sẽ "bốc hơi" khỏi danh sách người bán
                string sql = @"SELECT sp.*, dm.Ten AS TenDanhMuc 
                       FROM SanPham sp 
                       INNER JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang 
                       INNER JOIN DanhMuc dm ON sp.MaDanhMuc = dm.MaDanhMuc
                       WHERE ch.MaND = @maND 
                       AND sp.TrangThai <> N'Bị gỡ bởi Admin'";

                SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@maND", maND);

                con.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    ds.Add(new SanPham
                    {
                        MaSanPham = rdr["MaSanPham"].ToString(),
                        Ten = rdr["Ten"].ToString(),
                        GiaBan = Convert.ToDecimal(rdr["GiaBan"]),
                        HinhAnh = rdr["HinhAnh"].ToString(),
                        TrangThai = rdr["TrangThai"].ToString(),
                        TenDanhMuc = rdr["TenDanhMuc"] != DBNull.Value ? rdr["TenDanhMuc"].ToString() : "Chưa phân loại",
                        SoLuongTon = rdr["SoLuongTon"] != DBNull.Value ? Convert.ToInt32(rdr["SoLuongTon"]) : 0
                    });
                }
            }
            return View(ds);
        }

        // 2. Tính doanh thu
        public ActionResult DoanhThu()
        {
            // 1. Lấy thông tin người dùng đang đăng nhập từ Session
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (NguoiDung)Session["TaiKhoan"];
            string maND = user.MaND;

            decimal tongDoanhThu = 0;
            int soDonHangMoi = 0;
            string spBanChay = "Chưa có dữ liệu";

            using (SqlConnection con = new SqlConnection(constr))
            {
                con.Open();

                // --- QUERY 1: Tính tổng doanh thu của riêng Shop này ---
                // Chỉ tính các đơn 'Đã hoàn thành' hoặc 'Đã thanh toán'
                string sqlDoanhThu = @"SELECT SUM(ct.SoLuong * ct.DonGia) 
                               FROM ChiTietDonHang ct
                               JOIN DonHang dh ON ct.MaDon = dh.MaDon
                               JOIN SanPham sp ON ct.MaSanPham = sp.MaSanPham
                               JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang
                               WHERE ch.MaND = @maND AND dh.TrangThai = N'Đã hoàn thành'";

                SqlCommand cmd1 = new SqlCommand(sqlDoanhThu, con);
                cmd1.Parameters.AddWithValue("@maND", maND);
                var res1 = cmd1.ExecuteScalar();
                tongDoanhThu = res1 != DBNull.Value ? Convert.ToDecimal(res1) : 0;

                // --- QUERY 2: Đếm số đơn hàng mới (Chờ xử lý) của Shop ---
                string sqlDonHang = @"SELECT COUNT(DISTINCT dh.MaDon) 
                              FROM DonHang dh
                              JOIN ChiTietDonHang ct ON dh.MaDon = ct.MaDon
                              JOIN SanPham sp ON ct.MaSanPham = sp.MaSanPham
                              JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang
                              WHERE ch.MaND = @maND AND dh.TrangThai = N'Chờ xử lý'";

                SqlCommand cmd2 = new SqlCommand(sqlDonHang, con);
                cmd2.Parameters.AddWithValue("@maND", maND);
                soDonHangMoi = Convert.ToInt32(cmd2.ExecuteScalar());

                // --- QUERY 3: Tìm tên sản phẩm bán chạy nhất của Shop ---
                string sqlBestSeller = @"SELECT TOP 1 sp.Ten
                                 FROM ChiTietDonHang ct
                                 JOIN SanPham sp ON ct.MaSanPham = sp.MaSanPham
                                 JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang
                                 WHERE ch.MaND = @maND
                                 GROUP BY sp.Ten
                                 ORDER BY SUM(ct.SoLuong) DESC";

                SqlCommand cmd3 = new SqlCommand(sqlBestSeller, con);
                cmd3.Parameters.AddWithValue("@maND", maND);
                var res3 = cmd3.ExecuteScalar();
                if (res3 != null) spBanChay = res3.ToString();

                // --- QUERY 4: Lấy danh sách 5 giao dịch gần đây nhất của Shop ---
                List<DonHang> dsGiaoDich = new List<DonHang>();
                string sqlHistory = @"SELECT TOP 5 dh.MaDon, dh.NgayDat, SUM(ct.SoLuong * ct.DonGia) as TongTienDon
                      FROM DonHang dh
                      JOIN ChiTietDonHang ct ON dh.MaDon = ct.MaDon
                      JOIN SanPham sp ON ct.MaSanPham = sp.MaSanPham
                      JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang
                      WHERE ch.MaND = @maND AND dh.TrangThai = N'Đã hoàn thành'
                      GROUP BY dh.MaDon, dh.NgayDat
                      ORDER BY dh.NgayDat DESC";

                SqlCommand cmd4 = new SqlCommand(sqlHistory, con);
                cmd4.Parameters.AddWithValue("@maND", maND);
                using (SqlDataReader rdr = cmd4.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        dsGiaoDich.Add(new DonHang
                        {
                            MaDon = rdr["MaDon"].ToString(),
                            NgayDat = Convert.ToDateTime(rdr["NgayDat"]),
                            ThanhTien = Convert.ToDecimal(rdr["TongTienDon"])
                        });
                    }
                }
                // Truyền danh sách này sang View qua ViewBag
                ViewBag.LichSu = dsGiaoDich;
            }

            // Gán dữ liệu vào ViewBag để hiển thị lên View của bạn
            ViewBag.TongTien = tongDoanhThu;
            ViewBag.SoDonHang = soDonHangMoi;
            ViewBag.SpBanChay = spBanChay;

            return View();
        }
        public ActionResult ThemSanPham()
        {
            // Kiểm tra đăng nhập
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");

            // Lấy danh sách danh mục để đổ vào DropdownList
            ViewBag.MaDanhMuc = GetCategories();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ThemSanPham(SanPham sp, HttpPostedFileBase uploadAnh)
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (NguoiDung)Session["TaiKhoan"];

            using (SqlConnection con = new SqlConnection(constr))
            {
                con.Open();

                // 1. Lấy MaCuaHang (Giữ nguyên)
                string sqlCH = "SELECT MaCuaHang FROM CuaHang WHERE MaND = @maND";
                SqlCommand cmdCH = new SqlCommand(sqlCH, con);
                cmdCH.Parameters.AddWithValue("@maND", user.MaND);
                string maCH = cmdCH.ExecuteScalar()?.ToString();

                // 2. Xử lý Upload ảnh (Giữ nguyên)
                string fileName = "default.jpg";
                if (uploadAnh != null && uploadAnh.ContentLength > 0)
                {
                    fileName = Path.GetFileName(uploadAnh.FileName);
                    string path = Path.Combine(Server.MapPath("~/img/"), fileName);
                    uploadAnh.SaveAs(path);
                }

                // 3. Insert (CẦN SỬA ĐOẠN NÀY)
                // Thêm cột SoLuongTon vào câu lệnh INSERT
                string sql = @"INSERT INTO SanPham (MaSanPham, Ten, GiaBan, HinhAnh, MaDanhMuc, MaCuaHang, MoTa, SoLuongTon) 
                       VALUES (@ma, @ten, @gia, @anh, @loai, @mach, @mota, @soluong)";

                SqlCommand cmd = new SqlCommand(sql, con);

                cmd.Parameters.AddWithValue("@ma", SinhMaSanPham());
                cmd.Parameters.AddWithValue("@ten", sp.Ten);
                cmd.Parameters.AddWithValue("@loai", sp.MaDanhMuc);
                cmd.Parameters.AddWithValue("@mach", maCH);
                cmd.Parameters.AddWithValue("@gia", sp.GiaBan);
                cmd.Parameters.AddWithValue("@anh", fileName);
                cmd.Parameters.AddWithValue("@mota", sp.MoTa ?? "");

                // Thêm tham số số lượng (Nếu người dùng không nhập thì mặc định cho 100 để test)
                cmd.Parameters.AddWithValue("@soluong", sp.SoLuongTon > 0 ? sp.SoLuongTon : 100);

                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("KhoHang", new { maND = user.MaND });
        }

        // Hàm phụ lấy danh mục
        private List<SelectListItem> GetCategories()
        {
            List<SelectListItem> items = new List<SelectListItem>();
            using (SqlConnection con = new SqlConnection(constr))
            {
                string sql = "SELECT MaDanhMuc, Ten FROM DanhMuc";
                SqlCommand cmd = new SqlCommand(sql, con);
                con.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    items.Add(new SelectListItem { Text = rdr["Ten"].ToString(), Value = rdr["MaDanhMuc"].ToString() });
                }
            }
            return items;
        }
        public ActionResult SuaSanPham(string id)
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");

            SanPham sp = null;
            using (SqlConnection con = new SqlConnection(constr))
            {
                string sql = "SELECT * FROM SanPham WHERE MaSanPham = @id";
                SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@id", id);
                con.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    sp = new SanPham
                    {
                        MaSanPham = rdr["MaSanPham"].ToString(),
                        Ten = rdr["Ten"].ToString(),
                        MaDanhMuc = rdr["MaDanhMuc"].ToString(),
                        GiaBan = Convert.ToDecimal(rdr["GiaBan"]),
                        HinhAnh = rdr["HinhAnh"].ToString(),
                        MoTa = rdr["MoTa"].ToString()
                    };
                }
            }
            ViewBag.MaDanhMuc = GetCategories(); // Hàm lấy danh mục đã viết ở phần trước
            return View(sp);
        }

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult SuaSanPham(SanPham sp, HttpPostedFileBase uploadAnh, string anhCu)
        {
            // ... (Code kiểm tra đăng nhập giữ nguyên) ...

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();

                // LOGIC QUAN TRỌNG: Kiểm tra số lượng để ép trạng thái
                // Dù giao diện đã làm rồi, Server vẫn phải làm lại để tránh lỗi
                if (sp.SoLuongTon <= 0)
                {
                    sp.SoLuongTon = 0; // Đảm bảo không bị số âm
                    sp.TrangThai = "Ngừng bán";
                }
                else if (sp.TrangThai == "Ngừng bán" && sp.SoLuongTon > 0)
                {
                    // Trường hợp user quên chỉnh lại trạng thái
                    sp.TrangThai = "Đang bán";
                }

                // Xử lý ảnh (Giữ nguyên logic cũ của bạn)
                string tenAnh = anhCu;
                if (uploadAnh != null && uploadAnh.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(uploadAnh.FileName);
                    string path = Path.Combine(Server.MapPath("~/img/"), fileName);
                    uploadAnh.SaveAs(path);
                    tenAnh = fileName;
                }

                // Cập nhật Database (Thêm SoLuongTon và TrangThai)
                string sql = @"UPDATE SanPham 
                       SET Ten = @ten, 
                           MaDanhMuc = @madm, 
                           GiaBan = @gia, 
                           MoTa = @mota, 
                           HinhAnh = @hinh,
                           SoLuongTon = @sl,   -- Cập nhật số lượng
                           TrangThai = @tt     -- Cập nhật trạng thái
                       WHERE MaSanPham = @id";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ten", sp.Ten);
                cmd.Parameters.AddWithValue("@madm", sp.MaDanhMuc);
                cmd.Parameters.AddWithValue("@gia", sp.GiaBan);
                cmd.Parameters.AddWithValue("@mota", sp.MoTa ?? "");
                cmd.Parameters.AddWithValue("@hinh", tenAnh);
                cmd.Parameters.AddWithValue("@sl", sp.SoLuongTon); // Tham số số lượng
                cmd.Parameters.AddWithValue("@tt", sp.TrangThai);  // Tham số trạng thái
                cmd.Parameters.AddWithValue("@id", sp.MaSanPham);

                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("KhoHang");
        }

        // --- CHỨC NĂNG XÓA ---

        [HttpPost]
        public ActionResult XoaSanPham(string id)
        {
            var user = (NguoiDung)Session["TaiKhoan"];
            try
            {
                using (SqlConnection con = new SqlConnection(constr))
                {
                    string sql = "DELETE FROM SanPham WHERE MaSanPham = @id";
                    SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", id);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Nếu sản phẩm đã có trong đơn hàng thì không cho xóa (lỗi khóa ngoại)
                TempData["Error"] = "Không thể xóa sản phẩm này vì đã có khách hàng đặt mua!";
            }

            return RedirectToAction("KhoHang", new { maND = user.MaND });
        }
        // SỬA LẠI HÀM NÀY
        public ActionResult QuanLyDonHang()
        {
            // 1. Kiểm tra quyền quản lý
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (DoAn.Models.NguoiDung)Session["TaiKhoan"];
            string maND = user.MaND; // Mã của người chủ shop

            List<DoAn.Models.DonHang> dsDonHang = new List<DoAn.Models.DonHang>();

            using (SqlConnection conn = new SqlConnection(constr))
            {
                // --- CÂU LỆNH SQL THÔNG MINH HƠN ---
                // Lấy thêm cột "nd.DiaChi" (Địa chỉ trong hồ sơ) để dự phòng
                string sql = @"
            SELECT 
                dh.MaDon, 
                dh.NgayDat, 
                dh.TrangThai,
                dh.GhiChu,
                dh.TenNguoiNhan,
                dh.SDTNguoiNhan,
                dh.DiaChiGiaoHang,
                dh.TongTien,
                nd.HoTen AS TenTaiKhoan, 
                nd.SDT AS SDTTaiKhoan,
                nd.DiaChi AS DiaChiHoSo, -- <--- LẤY THÊM CÁI NÀY
                sp.Ten AS TenSanPham, 
                ct.SoLuong,
                ct.DonGia,
                (ct.SoLuong * ct.DonGia) AS ThanhTien
            FROM DonHang dh
            JOIN NguoiDung nd ON dh.MaKhachHang = nd.MaND
            JOIN ChiTietDonHang ct ON dh.MaDon = ct.MaDon
            JOIN SanPham sp ON ct.MaSanPham = sp.MaSanPham
            JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang
            WHERE ch.MaND = @maND
            ORDER BY dh.NgayDat DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@maND", maND);

                conn.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    // Xử lý Logic lấy địa chỉ: Ưu tiên đơn hàng -> Nếu rỗng thì lấy trong hồ sơ
                    string diaChiDon = rdr["DiaChiGiaoHang"] != DBNull.Value ? rdr["DiaChiGiaoHang"].ToString() : "";
                    string diaChiHoSo = rdr["DiaChiHoSo"] != DBNull.Value ? rdr["DiaChiHoSo"].ToString() : "";

                    // Kết quả cuối cùng
                    string diaChiHienThi = !string.IsNullOrEmpty(diaChiDon) ? diaChiDon :
                                           (!string.IsNullOrEmpty(diaChiHoSo) ? diaChiHoSo : "Chưa có địa chỉ");

                    // Xử lý Logic lấy SĐT tương tự
                    string sdtDon = rdr["SDTNguoiNhan"] != DBNull.Value ? rdr["SDTNguoiNhan"].ToString() : "";
                    string sdtHoSo = rdr["SDTTaiKhoan"] != DBNull.Value ? rdr["SDTTaiKhoan"].ToString() : "";
                    string sdtHienThi = !string.IsNullOrEmpty(sdtDon) ? sdtDon : sdtHoSo;

                    var dh = new DoAn.Models.DonHang
                    {
                        MaDon = rdr["MaDon"].ToString(),
                        NgayDat = Convert.ToDateTime(rdr["NgayDat"]),
                        TrangThai = rdr["TrangThai"].ToString(),
                        TenSanPham = rdr["TenSanPham"].ToString(),
                        SoLuong = Convert.ToInt32(rdr["SoLuong"]),
                        ThanhTien = Convert.ToDecimal(rdr["ThanhTien"]), // Tổng tiền món hàng

                        // Gán các thông tin đã xử lý ở trên
                        DiaChiGiaoHang = diaChiHienThi,
                        SDTNguoiNhan = sdtHienThi,
                        TenNguoiNhan = rdr["TenNguoiNhan"] != DBNull.Value ? rdr["TenNguoiNhan"].ToString() : rdr["TenTaiKhoan"].ToString(),

                        // Tạo đối tượng khách hàng lồng bên trong để hiển thị tên tài khoản đặt
                        KhachHang = new DoAn.Models.NguoiDung
                        {
                            HoTen = rdr["TenTaiKhoan"].ToString()
                        }
                    };
                    dsDonHang.Add(dh);
                }
            }
            return View(dsDonHang);
        }
        private string SinhMaSanPham()
        {
            string maMoi = "SP001";
            using (SqlConnection con = new SqlConnection(constr))
            {
                // Lấy mã lớn nhất hiện có. Lưu ý: Cột MaSanPham phải là kiểu chuỗi (nvarchar/varchar)
                string sql = "SELECT TOP 1 MaSanPham FROM SanPham ORDER BY MaSanPham DESC";
                SqlCommand cmd = new SqlCommand(sql, con);
                con.Open();
                var result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    string maHienTai = result.ToString(); // Ví dụ: SP005
                                                          // Cắt chuỗi lấy phần số (bỏ "SP"), sau đó cộng thêm 1
                    if (maHienTai.StartsWith("SP"))
                    {
                        int soThuTu = int.Parse(maHienTai.Substring(2));
                        maMoi = "SP" + (soThuTu + 1).ToString("D3"); // Kết quả: SP006
                    }
                }
            }
            return maMoi;
        }
        // Trong QuanLyController.cs hoặc GioHangController.cs
        [HttpPost]
        public ActionResult CapNhatTrangThai(string maDon, string trangThaiMoi)
        {
            var user = (DoAn.Models.NguoiDung)Session["TaiKhoan"];
            if (user == null) return RedirectToAction("DangNhap", "Home");

            string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // 1. Kiểm tra trạng thái hiện tại của đơn hàng trước khi cho phép hủy
                    string sqlCheck = "SELECT TrangThai FROM DonHang WHERE MaDon = @id";
                    SqlCommand cmdCheck = new SqlCommand(sqlCheck, conn, trans);
                    cmdCheck.Parameters.AddWithValue("@id", maDon);
                    string trangThaiHienTai = cmdCheck.ExecuteScalar().ToString();

                    // Nếu Người mua tự hủy mà đơn không còn ở trạng thái "Chờ xử lý" thì chặn lại
                    if (user.QuyenHan.Trim() == "NguoiMua" && trangThaiMoi == "Đã hủy" && trangThaiHienTai != "Chờ xử lý")
                    {
                        TempData["Message"] = "Đơn hàng đã được shop xử lý, bạn không thể tự hủy!";
                        return Redirect(Request.UrlReferrer.ToString());
                    }

                    // 2. Cập nhật trạng thái mới
                    string sqlUpdate = "UPDATE DonHang SET TrangThai = @status WHERE MaDon = @id";
                    SqlCommand cmdUpdate = new SqlCommand(sqlUpdate, conn, trans);
                    cmdUpdate.Parameters.AddWithValue("@status", trangThaiMoi);
                    cmdUpdate.Parameters.AddWithValue("@id", maDon);
                    cmdUpdate.ExecuteNonQuery();

                    // 3. Logic Hoàn Kho: Nếu trạng thái mới là 'Đã hủy' thì cộng lại số lượng tồn
                    if (trangThaiMoi == "Đã hủy")
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

                    trans.Commit();
                    TempData["Message"] = "Cập nhật trạng thái thành công!";
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    TempData["Message"] = "Lỗi: " + ex.Message;
                }
            }
            return Redirect(Request.UrlReferrer.ToString());
        }

        public string TaoMaDonHang()
        {
            string prefix = "DH";
            string moiMa = prefix + "001";

            using (SqlConnection con = new SqlConnection(constr))
            {
                // Chỉ lấy mã bắt đầu bằng 'DH' và có độ dài hợp lý (ví dụ dưới 10 ký tự)
                // để tránh lấy nhầm các mã cũ quá dài gây lỗi parse
                string sql = @"SELECT TOP 1 MaDon FROM DonHang 
                       WHERE MaDon LIKE 'DH%' AND LEN(MaDon) < 10 
                       ORDER BY LEN(MaDon) DESC, MaDon DESC";

                SqlCommand cmd = new SqlCommand(sql, con);
                con.Open();

                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    string maHienTai = result.ToString();

                    // Dùng TryParse để an toàn tuyệt đối, tránh crash nếu Substring lỗi
                    string phanSo = maHienTai.Substring(2);
                    long soTam; // Dùng long để chứa được số lớn hơn đề phòng
                    if (long.TryParse(phanSo, out soTam))
                    {
                        long soThuTu = soTam + 1;
                        moiMa = prefix + soThuTu.ToString("D3");
                    }
                }
            }
            return moiMa;
        }
        // Action mới: Chỉ hiển thị danh sách sản phẩm bị Admin gỡ
        public ActionResult SanPhamBiGo()
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (NguoiDung)Session["TaiKhoan"];
            string maND = user.MaND;

            List<SanPham> ds = new List<SanPham>();
            using (SqlConnection con = new SqlConnection(constr))
            {
                // Câu lệnh SQL này NGƯỢC LẠI với kho hàng
                // Chỉ lấy những cái có TrangThai = 'Bị gỡ bởi Admin'
                string sql = @"SELECT sp.*, dm.Ten AS TenDanhMuc 
                       FROM SanPham sp 
                       INNER JOIN CuaHang ch ON sp.MaCuaHang = ch.MaCuaHang 
                       INNER JOIN DanhMuc dm ON sp.MaDanhMuc = dm.MaDanhMuc
                       WHERE ch.MaND = @maND 
                       AND sp.TrangThai = N'Bị gỡ bởi Admin'";

                SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@maND", maND);

                con.Open();
                SqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    ds.Add(new SanPham
                    {
                        MaSanPham = rdr["MaSanPham"].ToString(),
                        Ten = rdr["Ten"].ToString(),
                        GiaBan = Convert.ToDecimal(rdr["GiaBan"]),
                        HinhAnh = rdr["HinhAnh"].ToString(),
                        TrangThai = rdr["TrangThai"].ToString(),
                        TenDanhMuc = rdr["TenDanhMuc"].ToString()
                    });
                }
            }
            return View(ds);
        }

        [HttpPost]
        public ActionResult XoaHangLoat(List<string> ids)
        {
            // 1. Kiểm tra dữ liệu đầu vào
            if (ids == null || ids.Count == 0)
            {
                return Json(new { success = false, message = "Không có mục nào được chọn" });
            }

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();

                foreach (var id in ids)
                {
                    try
                    {
                        // 2. Kiểm tra xem sản phẩm đã có đơn hàng chưa
                        // Nếu đã có trong ChiTietDonHang -> Không được xóa (để bảo toàn lịch sử đơn)
                        string checkSql = "SELECT COUNT(*) FROM ChiTietDonHang WHERE MaSanPham = @id";
                        SqlCommand checkCmd = new SqlCommand(checkSql, conn);
                        checkCmd.Parameters.AddWithValue("@id", id);
                        int count = (int)checkCmd.ExecuteScalar();

                        if (count > 0)
                        {
                            // Nếu đã bán rồi thì KHÔNG XÓA, chỉ chuyển trạng thái sang "Ngừng bán"
                            string softDelete = "UPDATE SanPham SET SoLuongTon = 0 WHERE MaSanPham = @id";
                            SqlCommand softCmd = new SqlCommand(softDelete, conn);
                            softCmd.Parameters.AddWithValue("@id", id);
                            softCmd.ExecuteNonQuery();
                        }
                        else
                        {
                            // 3. Nếu chưa bán bao giờ -> XÓA THẲNG KHỎI DB (Giống nút thùng rác của bạn)
                            string deleteSql = "DELETE FROM SanPham WHERE MaSanPham = @id";
                            SqlCommand delCmd = new SqlCommand(deleteSql, conn);
                            delCmd.Parameters.AddWithValue("@id", id);
                            delCmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception)
                    {
                        // Nếu lỗi thì bỏ qua, chạy tiếp cái sau
                        continue;
                    }
                }
            }

            // 4. Trả về kết quả thành công cho Ajax
            return Json(new { success = true });
        }
    }
}