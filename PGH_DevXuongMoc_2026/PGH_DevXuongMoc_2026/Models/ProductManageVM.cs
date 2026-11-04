using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace PGH_DevXuongMoc_2026.Models
{
    // Bảng trung gian để quản lý sản phẩm, biến thể sản phẩm và hình ảnh sản phẩm
    public class ProductManageVM
    {
        // Dùng để nhận dữ liệu từ form quản lý sản phẩm
        public Product Product { get; set; } = new Product();
        public List<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public List<ProductImage> Images { get; set; } = new List<ProductImage>();

        // Dùng để nhận file tải lên từ máy tính
        public IFormFile? PrimaryImageFile { get; set; }
        public List<IFormFile>? SecondaryImageFiles { get; set; }
    }
}