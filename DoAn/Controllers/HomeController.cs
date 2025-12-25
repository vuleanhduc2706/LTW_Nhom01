using DoAn.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
// using DoAn.Controllers; // Nếu KiemTraKhoaTaiKhoan nằm cùng namespace DoAn.Controllers thì ko cần dòng này

namespace DoAn.Controllers
{
    // --- BƯỚC 1: BỎ [KiemTraKhoaTaiKhoan] Ở ĐÂY ĐI ---
    // [KiemTraKhoaTaiKhoan] <--- XÓA DÒNG NÀY
    public class HomeController : Controller
    {
        private readonly string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;

        // Trang thông báo KHÔNG ĐƯỢC CHẶN (để người bị khóa còn vào xem được lỗi)
        public ActionResult ThongBaoKhoaTaiKhoan()
        {
            return View();
        }

        // Trang chủ ai cũng vào được -> KHÔNG CHẶN
        public ActionResult Index()
        {
            return View();
        }

        // Xem sản phẩm -> KHÔNG CHẶN
        public ActionResult SanPham(string id, string kw, string khoanggia)
        {
            // ... (Code giữ nguyên)
            var model = new DuLieu() { DanhSachDanhMuc = new List<DanhMuc>(), DanhSachSanPham = new List<SanPham>() };
            ViewBag.MaDanhMucChon = id;

            using (var conn = new SqlConnection(constr))
            {
                string query = "SELECT * FROM SanPham WHERE TrangThai <> N'Bị gỡ bởi Admin' ";
                SqlCommand cmd = new SqlCommand();

                if (!string.IsNullOrEmpty(id)) { query += " AND MaDanhMuc = @MaDanhMuc "; cmd.Parameters.AddWithValue("MaDanhMuc", id); }
                if (!string.IsNullOrEmpty(kw)) { query += " AND Ten LIKE @Ten "; cmd.Parameters.AddWithValue("Ten", "%" + kw + "%"); }
                if (!string.IsNullOrEmpty(khoanggia))
                {
                    var minmax = khoanggia.Split('-');
                    if (minmax.Length == 2)
                    {
                        if (decimal.TryParse(minmax[0], out decimal min)) { query += " AND GiaBan >= @min "; cmd.Parameters.AddWithValue("min", min); }
                        if (decimal.TryParse(minmax[1], out decimal max)) { query += " AND GiaBan <= @max "; cmd.Parameters.AddWithValue("max", max); }
                    }
                }
                cmd.CommandText = query; cmd.Connection = conn;
                SqlDataAdapter apt = new SqlDataAdapter(cmd);
                DataTable tb = new DataTable();
                apt.Fill(tb);
                foreach (DataRow dr in tb.Rows)
                {
                    model.DanhSachSanPham.Add(new SanPham
                    {
                        MaSanPham = dr["MaSanPham"].ToString(),
                        Ten = dr["Ten"].ToString(),
                        GiaBan = (decimal)dr["GiaBan"],
                        HinhAnh = dr["HinhAnh"].ToString(),
                        DonViTinh = dr["DonViTinh"].ToString(),
                        MoTa = dr["MoTa"].ToString(),
                        TrangThai = dr["TrangThai"].ToString(),
                        MaDanhMuc = dr["MaDanhMuc"].ToString(),
                        SoLuongTon = dr["SoLuongTon"] != DBNull.Value ? Convert.ToInt32(dr["SoLuongTon"]) : 0
                    });
                }
            }
            return View(model);
        }

        public ActionResult DanhMuc()
        {
            // ... (Code giữ nguyên)
            var model = new DuLieu() { DanhSachDanhMuc = new List<DanhMuc>() };
            using (var conn = new SqlConnection(constr))
            {
                string query = "SELECT * FROM DanhMuc";
                SqlCommand cmd = new SqlCommand(query, conn);
                SqlDataAdapter apt = new SqlDataAdapter(cmd);
                DataTable tb = new DataTable();
                apt.Fill(tb);
                foreach (DataRow dr in tb.Rows)
                {
                    model.DanhSachDanhMuc.Add(new DanhMuc { MaDanhMuc = dr["MaDanhMuc"].ToString(), Ten = dr["Ten"].ToString(), TrangThai = (bool)dr["TrangThai"] });
                }
            }
            return PartialView(model);
        }

        // --- AUTHENTICATION (Đăng ký/Đăng nhập ai cũng vào được) ---

        [HttpGet]
        public ActionResult DangKy() => View();

        [HttpPost]
        public ActionResult DangKy(NguoiDung nd, string QuyenHan, string repassword)
        {
            // ... (Code đăng ký giữ nguyên không đổi)
            // Kiểm tra mật khẩu nhập lại
            if (nd.MatKhau != repassword)
            {
                ViewBag.Error = "Mật khẩu nhập lại không khớp! Vui lòng kiểm tra lại.";
                return View();
            }
            if (string.IsNullOrEmpty(nd.SDT) || !Regex.IsMatch(nd.SDT, @"^0\d{9}$"))
            {
                ViewBag.Error = "Số điện thoại không hợp lệ! Phải có đúng 10 số và bắt đầu bằng số 0.";
                return View();
            }
            string constr = System.Web.Configuration.WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(constr))
            {
                try
                {
                    conn.Open();

                    // 1. Kiểm tra trùng Email hoặc SĐT
                    string checkSql = "SELECT COUNT(*) FROM NguoiDung WHERE Email=@email OR SDT=@sdt";
                    SqlCommand checkCmd = new SqlCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@email", nd.Email);
                    checkCmd.Parameters.AddWithValue("@sdt", nd.SDT);
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        ViewBag.Error = "Email hoặc Số điện thoại đã tồn tại!";
                        return View();
                    }

                    // 2. Tạo mã tự động tăng
                    string prefix = (QuyenHan == "NguoiMua") ? "KH" : "NB";
                    string moiMaND = prefix + "001";

                    string sqlMaxMa = "SELECT TOP 1 MaND FROM NguoiDung WHERE MaND LIKE @prefix + '%' ORDER BY MaND DESC";
                    SqlCommand cmdMax = new SqlCommand(sqlMaxMa, conn);
                    cmdMax.Parameters.AddWithValue("@prefix", prefix);
                    var resultMax = cmdMax.ExecuteScalar();

                    if (resultMax != null)
                    {
                        string maHienTai = resultMax.ToString();
                        int soThuTu = int.Parse(maHienTai.Substring(2)) + 1;
                        moiMaND = prefix + soThuTu.ToString("D3");
                    }

                    // 3. Thiết lập trạng thái
                    int st = 1;
                    if (QuyenHan == "NguoiBan") st = 2; // Chờ duyệt

                    // 4. Thực hiện Insert
                    string sql = @"INSERT INTO NguoiDung (MaND, HoTen, SDT, Email, MatKhau, QuyenHan, TrangThai) 
                            VALUES (@ma, @ten, @sdt, @email, @pw, @quyen, @status)";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ma", moiMaND);
                    cmd.Parameters.AddWithValue("@ten", nd.HoTen);
                    cmd.Parameters.AddWithValue("@sdt", nd.SDT);
                    cmd.Parameters.AddWithValue("@email", nd.Email);
                    cmd.Parameters.AddWithValue("@pw", nd.MatKhau);
                    cmd.Parameters.AddWithValue("@quyen", QuyenHan);
                    cmd.Parameters.AddWithValue("@status", st);

                    cmd.ExecuteNonQuery();

                    // Insert CuaHang nếu là Người Bán
                    if (QuyenHan == "NguoiBan")
                    {
                        string sqlMaxCH = "SELECT TOP 1 MaCuaHang FROM CuaHang ORDER BY MaCuaHang DESC";
                        SqlCommand cmdMaxCH = new SqlCommand(sqlMaxCH, conn);
                        var maxCH = cmdMaxCH.ExecuteScalar();
                        string moiMaCH = "CH001";
                        if (maxCH != null)
                        {
                            moiMaCH = "CH" + (int.Parse(maxCH.ToString().Substring(2)) + 1).ToString("D3");
                        }

                        string sqlCH = "INSERT INTO CuaHang (MaCuaHang, TenCuaHang, MaND, TrangThaiCH) VALUES (@maCH, @tenCH, @maND, 0)";
                        SqlCommand cmdCH = new SqlCommand(sqlCH, conn);
                        cmdCH.Parameters.AddWithValue("@maCH", moiMaCH);
                        cmdCH.Parameters.AddWithValue("@tenCH", "Cửa hàng của " + nd.HoTen);
                        cmdCH.Parameters.AddWithValue("@maND", moiMaND);
                        cmdCH.ExecuteNonQuery();
                    }

                    return RedirectToAction("DangNhap");
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "Lỗi hệ thống: " + ex.Message;
                }
            }
            return View();
        }

        [HttpGet]
        public ActionResult DangNhap() => View();

        [HttpPost]
        public ActionResult DangNhap(string account, string matkhau)
        {
            // ... (Code đăng nhập giữ nguyên)
            using (SqlConnection conn = new SqlConnection(constr))
            {
                // 1. Kiểm tra tài khoản (Email hoặc SĐT) và Mật khẩu
                string query = "SELECT * FROM NguoiDung WHERE (Email=@acc OR SDT=@acc) AND MatKhau=@pw";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@acc", account); // <--- Lưu ý tên biến là 'account'
                cmd.Parameters.AddWithValue("@pw", matkhau);

                SqlDataAdapter apt = new SqlDataAdapter(cmd);
                DataTable tb = new DataTable();
                apt.Fill(tb);

                if (tb.Rows.Count > 0)
                {
                    // Lấy trạng thái
                    int trangThai = tb.Rows[0]["TrangThai"] != DBNull.Value ? Convert.ToInt32(tb.Rows[0]["TrangThai"]) : 1;

                    // TRƯỜNG HỢP 1: Bị khóa
                    if (trangThai == 0)
                    {
                        ViewBag.Error = "Tài khoản đã bị khóa do vi phạm!";
                        return View();
                    }
                    // TRƯỜNG HỢP 2: Chờ duyệt
                    else if (trangThai == 2)
                    {
                        ViewBag.Error = "Tài khoản đang chờ Admin duyệt.";
                        return View();
                    }

                    // TRƯỜNG HỢP 3: Đăng nhập thành công -> LƯU SESSION ĐẦY ĐỦ
                    var user = new DoAn.Models.NguoiDung();
                    user.MaND = tb.Rows[0]["MaND"].ToString();
                    user.HoTen = tb.Rows[0]["HoTen"].ToString();
                    user.QuyenHan = tb.Rows[0]["QuyenHan"].ToString().Trim();

                    // --- ĐOẠN QUAN TRỌNG: LƯU ĐỊA CHỈ VÀO SESSION ---
                    // Nếu không có đoạn này, mỗi lần đăng nhập lại sẽ bị mất địa chỉ
                    user.DiaChi = tb.Rows[0]["DiaChi"] != DBNull.Value ? tb.Rows[0]["DiaChi"].ToString() : "";
                    user.SDT = tb.Rows[0]["SDT"] != DBNull.Value ? tb.Rows[0]["SDT"].ToString() : "";
                    user.Email = tb.Rows[0]["Email"] != DBNull.Value ? tb.Rows[0]["Email"].ToString() : "";
                    // ------------------------------------------------

                    Session["TaiKhoan"] = user;
                    Session["User"] = user.HoTen;

                    // Điều hướng theo quyền
                    if (user.QuyenHan == "Admin" || user.QuyenHan == "NguoiBan")
                    {
                        return RedirectToAction("KhoHang", "QuanLy");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng!";
            }
            return View();
        }

        public ActionResult DangXuat() { Session.Clear(); return RedirectToAction("Index"); }

        // --- CÁC HÀM CÁ NHÂN CẦN BẢO VỆ (DÁN BÙA Ở ĐÂY) ---

        [KiemTraKhoaTaiKhoan] // <--- CHỈ DÁN VÀO ĐÂY
        public ActionResult ThongTinCaNhan()
        {
            // ... (Code giữ nguyên)
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap");
            var userSession = (NguoiDung)Session["TaiKhoan"];

            ProfileViewModel model = new ProfileViewModel();

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();

                // Lấy thông tin người dùng (Bao gồm Địa chỉ mới thêm)
                string sqlUser = "SELECT * FROM NguoiDung WHERE MaND = @id";
                SqlCommand cmd = new SqlCommand(sqlUser, conn);
                cmd.Parameters.AddWithValue("@id", userSession.MaND);

                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        model.MaND = rdr["MaND"].ToString();
                        model.HoTen = rdr["HoTen"].ToString();
                        model.SDT = rdr["SDT"].ToString();
                        model.Email = rdr["Email"].ToString();
                        // Lấy địa chỉ, kiểm tra null
                        model.DiaChi = rdr["DiaChi"] != DBNull.Value ? rdr["DiaChi"].ToString() : "";
                        model.QuyenHan = rdr["QuyenHan"].ToString().Trim();
                    }
                }

                // Nếu là Người bán, lấy thêm thông tin Shop
                if (model.QuyenHan == "NguoiBan")
                {
                    string sqlShop = "SELECT * FROM CuaHang WHERE MaND = @id";
                    SqlCommand cmdShop = new SqlCommand(sqlShop, conn);
                    cmdShop.Parameters.AddWithValue("@id", userSession.MaND);
                    using (var rdrShop = cmdShop.ExecuteReader())
                    {
                        if (rdrShop.Read())
                        {
                            model.TenCuaHang = rdrShop["TenCuaHang"].ToString();
                            model.DiaChiCuaHang = rdrShop["DiaChi"] != DBNull.Value ? rdrShop["DiaChi"].ToString() : "";
                        }
                    }
                }
            }
            return View(model);
        }

        [KiemTraKhoaTaiKhoan] // <--- DÁN VÀO ĐÂY
        [HttpPost]
        public ActionResult CapNhatThongTin(ProfileViewModel model)
        {
            // ... (Code giữ nguyên)
            // 1. Kiểm tra đăng nhập
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();

                try
                {
                    // A. Cập nhật bảng NguoiDung (SQL)
                    string sqlUser = "UPDATE NguoiDung SET HoTen=@ten, SDT=@sdt, Email=@email, DiaChi=@diachi WHERE MaND=@ma";
                    SqlCommand cmdUser = new SqlCommand(sqlUser, conn, trans);
                    cmdUser.Parameters.AddWithValue("@ten", model.HoTen);
                    cmdUser.Parameters.AddWithValue("@sdt", model.SDT);
                    cmdUser.Parameters.AddWithValue("@email", model.Email);
                    cmdUser.Parameters.AddWithValue("@diachi", model.DiaChi ?? ""); // Xử lý nếu để trống
                    cmdUser.Parameters.AddWithValue("@ma", model.MaND);
                    cmdUser.ExecuteNonQuery();

                    // B. Nếu là Người bán, cập nhật thêm bảng CuaHang
                    if (model.QuyenHan == "NguoiBan")
                    {
                        string sqlShop = "UPDATE CuaHang SET TenCuaHang = @tenCH, DiaChi = @diachiCH WHERE MaND = @ma";
                        SqlCommand cmdShop = new SqlCommand(sqlShop, conn, trans);
                        // Kiểm tra null để tránh lỗi nếu model không có dữ liệu cửa hàng
                        cmdShop.Parameters.AddWithValue("@tenCH", model.TenCuaHang ?? "");
                        cmdShop.Parameters.AddWithValue("@diachiCH", model.DiaChiCuaHang ?? "");
                        cmdShop.Parameters.AddWithValue("@ma", model.MaND);
                        cmdShop.ExecuteNonQuery();
                    }

                    // Xác nhận lưu xuống Database
                    trans.Commit();

                    // --- C. CẬP NHẬT LẠI SESSION (QUAN TRỌNG NHẤT) ---
                    // Bước này giúp trang Thanh Toán nhận được địa chỉ mới ngay lập tức
                    var userSession = (NguoiDung)Session["TaiKhoan"];

                    userSession.HoTen = model.HoTen;
                    userSession.SDT = model.SDT;        // Cập nhật SĐT mới
                    userSession.Email = model.Email;    // Cập nhật Email mới
                    userSession.DiaChi = model.DiaChi;  // Cập nhật Địa chỉ mới

                    // Gán ngược lại vào biến Session chính
                    Session["TaiKhoan"] = userSession;
                    Session["User"] = model.HoTen;

                    TempData["Success"] = "Cập nhật thông tin thành công!";
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                }
            }

            // Quay lại trang hồ sơ để xem kết quả
            return RedirectToAction("ThongTinCaNhan");
        }

        [HttpGet]
        public ActionResult QuenMatKhau() => View();

        [HttpPost]
        public ActionResult QuenMatKhau(string email)
        {
            // ... (Code gửi mail giữ nguyên)
            string matKhau = "";
            string hoTen = "";

            // 1. Kiểm tra Email có tồn tại không
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                string sql = "SELECT HoTen, MatKhau FROM NguoiDung WHERE Email = @email";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@email", email);
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        hoTen = rdr["HoTen"].ToString();
                        matKhau = rdr["MatKhau"].ToString();
                    }
                    else
                    {
                        ViewBag.Error = "Email này chưa đăng ký!";
                        return View();
                    }
                }
            }

            // 2. Gửi Email (Đã tối ưu để tránh Spam)
            try
            {
                var fromAddress = new MailAddress("vuleanhduc2706@gmail.com", "Cakey Bakery Support"); // THAY EMAIL CỦA BẠN VÀO ĐÂY
                var toAddress = new MailAddress(email, hoTen);
                string fromPassword = "kqgp owpd ylnz bcul"; // THAY MẬT KHẨU ỨNG DỤNG VÀO ĐÂY
                string subject = "Yêu cầu cấp lại mật khẩu"; // Tiêu đề nghiêm túc, không viết hoa toàn bộ

                // Nội dung Email trình bày chuyên nghiệp (HTML)
                // Các bộ lọc Spam thích email có cấu trúc rõ ràng hơn là text trơn
                string body = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #e0e0e0; border-radius: 5px; max-width: 600px;'>
                <h2 style='color: #d35400;'>Xin chào {hoTen},</h2>
                <p>Chúng tôi nhận được yêu cầu lấy lại mật khẩu cho tài khoản của bạn tại <b>Cakey Bakery</b>.</p>
                <div style='background-color: #f9f9f9; padding: 15px; margin: 20px 0; border-left: 4px solid #d35400;'>
                    <p style='margin: 0;'>Mật khẩu hiện tại của bạn là:</p>
                    <p style='font-size: 24px; font-weight: bold; color: #333; margin: 10px 0;'>{matKhau}</p>
                </div>
                <p>Vui lòng đăng nhập và đổi mật khẩu ngay để bảo mật tài khoản.</p>
                <hr style='border: none; border-top: 1px solid #eee;' />
                <p style='font-size: 12px; color: #777;'>Nếu bạn không yêu cầu điều này, vui lòng bỏ qua email này.<br/>Đội ngũ hỗ trợ Cakey Bakery.</p>
            </div>
        ";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword),
                    Timeout = 20000
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    Priority = MailPriority.Normal // Không set High, dễ bị coi là Spam
                })
                {
                    smtp.Send(message);
                }

                ViewBag.Success = "Đã gửi mật khẩu vào email. Vui lòng kiểm tra Inbox!";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Không gửi được email: " + ex.Message;
            }

            return View();
        }

        // --- CÁC HÀM GIỎ HÀNG THÌ KHÔNG CẦN CHẶN Ở ĐÂY (VÌ ĐÃ CHẶN Ở GIOHANGCONTROLLER RỒI) ---
        // Tuy nhiên nếu bạn để chung file thì phải chặn.
        // NHƯNG TỐT NHẤT: Bạn nên dùng file GioHangController.cs riêng mà tôi đã gửi ở trên.
        // Nếu bạn vẫn muốn dùng chung thì thêm [KiemTraKhoaTaiKhoan] vào DatHang, ThemGioHang...

        // ... (Các hàm giỏ hàng cũ nếu còn giữ lại thì cứ để đó, nhưng nên xóa bớt nếu đã chuyển sang GioHangController)
        public ActionResult ThemGioHang(string ms, string strURL)
        {
            // Lấy giỏ hàng từ Session (Dictionary<MaSP, SoLuong>)
            Dictionary<string, int> giohang = Session["giohang"] as Dictionary<string, int> ?? new Dictionary<string, int>();

            if (giohang.ContainsKey(ms))
                giohang[ms]++;
            else
                giohang.Add(ms, 1);

            Session["giohang"] = giohang;
            return Redirect(strURL);
        }

        public ActionResult XoaGioHang(string maSP)
        {
            Dictionary<string, int> giohang = Session["giohang"] as Dictionary<string, int>;
            if (giohang != null && giohang.ContainsKey(maSP))
            {
                giohang.Remove(maSP);
                Session["giohang"] = giohang;
            }
            return RedirectToAction("CheckOut");
        }

        [HttpPost]
        public ActionResult CapNhatGioHang(string maSP, int txtSoLuong)
        {
            Dictionary<string, int> giohang = Session["giohang"] as Dictionary<string, int>;
            if (giohang != null && giohang.ContainsKey(maSP))
            {
                if (txtSoLuong > 0) giohang[maSP] = txtSoLuong;
                else giohang.Remove(maSP);
                Session["giohang"] = giohang;
            }
            return RedirectToAction("CheckOut");
        }

        // --- THANH TOÁN & ĐƠN HÀNG ---

        public ActionResult CheckOut()
        {
            var giohang = Session["giohang"] as Dictionary<string, int>;
            if (giohang == null || giohang.Count == 0)
            {
                return RedirectToAction("SanPham"); // Nếu giỏ hàng trống mới quay về danh sách SP
            }

            List<ItemGioHang> danhSachDonHang = new List<ItemGioHang>();
            decimal tongTien = 0;

            using (var conn = new SqlConnection(constr))
            {
                string ids = string.Join("','", giohang.Keys);
                string query = $"SELECT * FROM SanPham WHERE MaSanPham IN ('{ids}')";
                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    string maSP = dr["MaSanPham"].ToString();
                    int soLuong = giohang[maSP];
                    decimal gia = (decimal)dr["GiaBan"];
                    tongTien += (gia * soLuong);

                    danhSachDonHang.Add(new ItemGioHang
                    {
                        MaSanPham = maSP,
                        TenSanPham = dr["Ten"].ToString(),
                        HinhAnh = dr["HinhAnh"].ToString(),
                        DonGia = gia,
                        SoLuong = soLuong
                        // Không gán ThanhTien ở đây nếu nó là thuộc tính tự tính (Read-only)
                    });
                }
            }
            ViewBag.TongTien = tongTien;
            return View(danhSachDonHang); // Trả về List<ItemGioHang> cho View
        }

        [HttpPost]
        public ActionResult DatHang(string GhiChu)
        {
            // 1. Kiểm tra xem đã đăng nhập chưa
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "Home");
            var user = (DoAn.Models.NguoiDung)Session["TaiKhoan"];

            // 2. Kiểm tra địa chỉ (Bắt buộc phải có địa chỉ mới cho đặt)
            if (string.IsNullOrEmpty(user.DiaChi))
            {
                TempData["Message"] = "Bạn cần cập nhật địa chỉ trước khi đặt hàng!";
                return RedirectToAction("ThongTinCaNhan", "Home"); // Hoặc trang sửa hồ sơ của bạn
            }

            // 3. Lấy giỏ hàng từ Session
            var giohang = Session["giohang"] as Dictionary<string, int>;
            if (giohang == null || giohang.Count == 0) return RedirectToAction("Index", "Home");

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlTransaction tran = conn.BeginTransaction();

                try
                {
                    // --- A. KIỂM TRA TỒN KHO LẦN CUỐI ---
                    foreach (var item in giohang)
                    {
                        string sqlCheck = "SELECT Ten, SoLuongTon FROM SanPham WHERE MaSanPham = @id";
                        SqlCommand cmdCheck = new SqlCommand(sqlCheck, conn, tran);
                        cmdCheck.Parameters.AddWithValue("@id", item.Key);

                        using (SqlDataReader rdr = cmdCheck.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                int tonKho = Convert.ToInt32(rdr["SoLuongTon"]);
                                if (item.Value > tonKho)
                                {
                                    // Nếu mua lố số lượng -> Báo lỗi & Hủy
                                    ViewBag.Error = "Sản phẩm " + rdr["Ten"] + " chỉ còn " + tonKho + " cái.";
                                    rdr.Close(); // Đóng reader trước khi return
                                    tran.Rollback();
                                    return View("CheckOut", LayGioHangDeHienThi(giohang));
                                }
                            }
                        }
                    }

                    // --- B. TẠO ĐƠN HÀNG (LẤY TỪ SESSION USER) ---
                    string maDH = "DH" + DateTime.Now.ToString("ddHHmmss"); // Tạo mã tự động

                    string sqlDH = @"INSERT INTO DonHang (MaDon, MaKhachHang, NgayDat, GhiChu, TongTien, TrangThai, TenNguoiNhan, SDTNguoiNhan, DiaChiGiaoHang) 
                             VALUES (@ma, @makh, GETDATE(), @ghichu, 0, N'Chờ xử lý', @ten, @sdt, @diachi)";

                    SqlCommand cmdDH = new SqlCommand(sqlDH, conn, tran);
                    cmdDH.Parameters.AddWithValue("@ma", maDH);
                    cmdDH.Parameters.AddWithValue("@makh", user.MaND);
                    cmdDH.Parameters.AddWithValue("@ghichu", GhiChu ?? "");

                    // LẤY DỮ LIỆU TỪ BIẾN USER (SESSION)
                    cmdDH.Parameters.AddWithValue("@ten", user.HoTen);
                    cmdDH.Parameters.AddWithValue("@sdt", user.SDT);
                    cmdDH.Parameters.AddWithValue("@diachi", user.DiaChi);

                    cmdDH.ExecuteNonQuery();

                    // --- C. TẠO CHI TIẾT & TRỪ KHO ---
                    decimal tongTien = 0;
                    foreach (var item in giohang)
                    {
                        // Lấy giá bán
                        string sqlGia = "SELECT GiaBan FROM SanPham WHERE MaSanPham = @id";
                        SqlCommand cmdGia = new SqlCommand(sqlGia, conn, tran);
                        cmdGia.Parameters.AddWithValue("@id", item.Key);
                        decimal donGia = (decimal)cmdGia.ExecuteScalar();
                        tongTien += (donGia * item.Value);

                        // Insert chi tiết
                        string sqlCT = "INSERT INTO ChiTietDonHang (MaDon, MaSanPham, SoLuong, DonGia) VALUES (@ma, @sp, @sl, @gia)";
                        SqlCommand cmdCT = new SqlCommand(sqlCT, conn, tran);
                        cmdCT.Parameters.AddWithValue("@ma", maDH);
                        cmdCT.Parameters.AddWithValue("@sp", item.Key);
                        cmdCT.Parameters.AddWithValue("@sl", item.Value);
                        cmdCT.Parameters.AddWithValue("@gia", donGia);
                        cmdCT.ExecuteNonQuery();

                        // Trừ kho
                        string sqlTru = "UPDATE SanPham SET SoLuongTon = SoLuongTon - @sl WHERE MaSanPham = @sp";
                        SqlCommand cmdTru = new SqlCommand(sqlTru, conn, tran);
                        cmdTru.Parameters.AddWithValue("@sl", item.Value);
                        cmdTru.Parameters.AddWithValue("@sp", item.Key);
                        cmdTru.ExecuteNonQuery();
                    }

                    // Update tổng tiền
                    string sqlUpd = "UPDATE DonHang SET TongTien = @tong WHERE MaDon = @ma";
                    SqlCommand cmdUpd = new SqlCommand(sqlUpd, conn, tran);
                    cmdUpd.Parameters.AddWithValue("@tong", tongTien);
                    cmdUpd.Parameters.AddWithValue("@ma", maDH);
                    cmdUpd.ExecuteNonQuery();

                    tran.Commit();

                    // --- QUAN TRỌNG: XÓA GIỎ HÀNG ---
                    Session["giohang"] = null;

                    return RedirectToAction("XacNhanDonHang"); // Chuyển sang trang thông báo thành công
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    ViewBag.Error = "Lỗi hệ thống: " + ex.Message;
                    return View("CheckOut", LayGioHangDeHienThi(giohang));
                }
            }
        }

        // Hàm phụ trợ (Copy để vào cùng file Controller nếu chưa có)
        private List<DoAn.Models.ItemGioHang> LayGioHangDeHienThi(Dictionary<string, int> giohang)
        {
            var list = new List<DoAn.Models.ItemGioHang>();

            // Kiểm tra nếu giỏ hàng rỗng thì trả về list rỗng ngay
            if (giohang == null || giohang.Count == 0)
            {
                return list;
            }

            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                foreach (var item in giohang)
                {
                    // Lấy thông tin từng sản phẩm dựa trên ID (item.Key)
                    string sql = "SELECT * FROM SanPham WHERE MaSanPham = @id";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", item.Key);

                    SqlDataReader rdr = cmd.ExecuteReader();
                    if (rdr.Read())
                    {
                        list.Add(new DoAn.Models.ItemGioHang
                        {
                            MaSanPham = item.Key,
                            TenSanPham = rdr["Ten"].ToString(),
                            HinhAnh = rdr["HinhAnh"].ToString(),

                            // Gán Đơn giá và Số lượng -> ThanhTien sẽ tự động tính
                            DonGia = Convert.ToDecimal(rdr["GiaBan"]),
                            SoLuong = item.Value
                        });
                    }
                    rdr.Close(); // Đóng reader để tiếp tục vòng lặp
                }
            }
            return list;
        }

        public ActionResult ChiTiet(string id)
        {
            // ... (Code giữ nguyên)
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("SanPham");
            }

            DuLieuModel model = new DuLieuModel();
            model.Products = new List<SanPham>(); // Khởi tạo danh sách sản phẩm liên quan

            using (SqlConnection con = new SqlConnection(constr))
            {
                // 1. Truy vấn lấy thông tin sản phẩm chi tiết
                string sqlDetail = "SELECT * FROM SanPham WHERE MaSanPham = @id AND TrangThai <> N'Bị gỡ bởi Admin'";
                SqlCommand cmdDetail = new SqlCommand(sqlDetail, con);
                cmdDetail.Parameters.AddWithValue("@id", id);

                con.Open();
                SqlDataReader rdr = cmdDetail.ExecuteReader();

                if (rdr.Read())
                {
                    model.Product = new SanPham
                    {
                        MaSanPham = rdr["MaSanPham"].ToString(),
                        Ten = rdr["Ten"].ToString(),
                        GiaBan = Convert.ToDecimal(rdr["GiaBan"]),
                        HinhAnh = rdr["HinhAnh"].ToString(),
                        MoTa = rdr["MoTa"].ToString(),
                        MaDanhMuc = rdr["MaDanhMuc"].ToString()
                    };
                }
                rdr.Close();

                // 2. Truy vấn lấy sản phẩm liên quan (cùng danh mục, trừ sản phẩm hiện tại)
                if (model.Product != null)
                {
                    string sqlRelated = "SELECT TOP 4 * FROM SanPham WHERE MaDanhMuc = @maDM AND MaSanPham <> @maSP";
                    SqlCommand cmdRelated = new SqlCommand(sqlRelated, con);
                    cmdRelated.Parameters.AddWithValue("@maDM", model.Product.MaDanhMuc);
                    cmdRelated.Parameters.AddWithValue("@maSP", id);

                    SqlDataReader rdrRel = cmdRelated.ExecuteReader();
                    while (rdrRel.Read())
                    {
                        model.Products.Add(new SanPham
                        {
                            MaSanPham = rdrRel["MaSanPham"].ToString(),
                            Ten = rdrRel["Ten"].ToString(),
                            GiaBan = Convert.ToDecimal(rdrRel["GiaBan"]),
                            HinhAnh = rdrRel["HinhAnh"].ToString()
                        });
                    }
                    rdrRel.Close();
                }
            }

            if (model.Product == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        public ActionResult XacNhanDonHang() => View();
    }
}