using System;
using System.Web;
using System.Web.Mvc; // Thư viện quan trọng để làm Filter
using System.Data.SqlClient;
using System.Web.Configuration;

namespace DoAn.Controllers
{
    // 1. Kế thừa từ ActionFilterAttribute để biến class thường thành "Ông bảo vệ"
    public class KiemTraKhoaTaiKhoan : ActionFilterAttribute
    {
        // 2. Ghi đè hàm OnActionExecuting (Hàm này chạy TRƯỚC khi vào Controller)
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // --- ĐOẠN 1: LẤY USER TỪ SESSION ---
            var userSession = HttpContext.Current.Session["TaiKhoan"] as DoAn.Models.NguoiDung;

            // Chỉ kiểm tra nếu người dùng ĐÃ ĐĂNG NHẬP
            if (userSession != null)
            {
                // --- ĐOẠN 2: SOI DATABASE XEM CÓ BỊ KHÓA KHÔNG ---
                string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(constr))
                {
                    conn.Open();
                    string sql = "SELECT TrangThai FROM NguoiDung WHERE MaND = @ma";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ma", userSession.MaND);

                    object result = cmd.ExecuteScalar();

                    if (result != null)
                    {
                        int trangThai = Convert.ToInt32(result);

                        // --- ĐOẠN 3: NẾU BỊ KHÓA (Status = 0) THÌ CHẶN LẠI ---
                        if (trangThai == 0)
                        {
                            // Xóa session để đăng xuất
                            HttpContext.Current.Session.Clear();
                            HttpContext.Current.Session.Abandon();

                            // Điều hướng sang trang thông báo (Thay vì cho vào Controller)
                            filterContext.Result = new RedirectResult("/Home/ThongBaoKhoaTaiKhoan");
                            return;
                        }
                    }
                }
            }

            // Nếu không bị khóa, cho phép đi tiếp
            base.OnActionExecuting(filterContext);
        }
    }
}