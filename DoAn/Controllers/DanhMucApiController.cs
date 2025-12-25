using DoAn.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Configuration;
using System.Web.Http;

namespace DoAn.Controllers
{
    [KiemTraKhoaTaiKhoan]
    public class DanhMucApiController : ApiController
    {
        // Chuỗi kết nối lấy từ Web.config
        string constr = WebConfigurationManager.ConnectionStrings["BanhNgot"].ConnectionString;

        // 1. HIỂN THỊ: Lấy danh sách danh mục (GET: api/DanhMucApi)
        public IEnumerable<DanhMuc> Get()
        {
            List<DanhMuc> list = new List<DanhMuc>();
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM DanhMuc", conn);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new DanhMuc
                    {
                        MaDanhMuc = dr["MaDanhMuc"].ToString(),
                        Ten = dr["Ten"].ToString(),
                        MoTa = dr["MoTa"] != DBNull.Value ? dr["MoTa"].ToString() : "",
                        TrangThai = Convert.ToBoolean(dr["TrangThai"])
                    });
                }
            }
            return list;
        }

        // 2. HIỂN THỊ CHI TIẾT: Lấy 1 danh mục (GET: api/DanhMucApi/DMC001)
        public IHttpActionResult Get(string id)
        {
            DanhMuc dm = null;
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM DanhMuc WHERE MaDanhMuc = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.Read())
                {
                    dm = new DanhMuc
                    {
                        MaDanhMuc = dr["MaDanhMuc"].ToString(),
                        Ten = dr["Ten"].ToString(),
                        MoTa = dr["MoTa"] != DBNull.Value ? dr["MoTa"].ToString() : "",
                        TrangThai = Convert.ToBoolean(dr["TrangThai"])
                    };
                }
            }
            if (dm == null) return NotFound();
            return Ok(dm);
        }

        // 3. THÊM: Thêm danh mục mới (POST: api/DanhMucApi)
        [HttpPost]
        public IHttpActionResult Post(DanhMuc dm)
        {
            if (!ModelState.IsValid) return BadRequest("Dữ liệu không hợp lệ");
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                try
                {
                    string sql = "INSERT INTO DanhMuc(MaDanhMuc, Ten, MoTa, TrangThai) VALUES(@Id, @Ten, @MoTa, @TT)";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", dm.MaDanhMuc);
                    cmd.Parameters.AddWithValue("@Ten", dm.Ten);
                    cmd.Parameters.AddWithValue("@MoTa", (object)dm.MoTa ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TT", dm.TrangThai);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    return BadRequest("Lỗi (có thể trùng mã): " + ex.Message);
                }
            }
            return Ok("Thêm thành công!");
        }

        // 4. SỬA: Cập nhật danh mục (PUT: api/DanhMucApi)
        [HttpPut]
        public IHttpActionResult Put(DanhMuc dm)
        {
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                string sql = "UPDATE DanhMuc SET Ten=@Ten, MoTa=@MoTa, TrangThai=@TT WHERE MaDanhMuc=@Id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", dm.MaDanhMuc);
                cmd.Parameters.AddWithValue("@Ten", dm.Ten);
                cmd.Parameters.AddWithValue("@MoTa", (object)dm.MoTa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TT", dm.TrangThai);

                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return NotFound(); // Không tìm thấy mã để sửa
            }
            return Ok("Cập nhật thành công!");
        }

        // 5. XÓA: Xóa danh mục (DELETE: api/DanhMucApi/DMC001)
        [HttpDelete]
        public IHttpActionResult Delete(string id)
        {
            using (SqlConnection conn = new SqlConnection(constr))
            {
                conn.Open();
                // Kiểm tra ràng buộc khóa ngoại (Nếu danh mục đã có sản phẩm thì ko được xóa)
                SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM SanPham WHERE MaDanhMuc=@id", conn);
                checkCmd.Parameters.AddWithValue("@id", id);
                int count = (int)checkCmd.ExecuteScalar();
                if (count > 0) return BadRequest("Danh mục này đang có sản phẩm, không thể xóa!");

                // Nếu an toàn thì xóa
                SqlCommand cmd = new SqlCommand("DELETE FROM DanhMuc WHERE MaDanhMuc = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return NotFound();
            }
            return Ok("Xóa thành công!");
        }
    }
}