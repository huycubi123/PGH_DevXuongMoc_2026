using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PGH_DevXuongMoc_2026.Models;

namespace PGH_DevXuongMoc_2026.Areas.Admins.Controllers
{
    [Area("Admins")]
    public class ProductsController : Controller
    {
        private readonly XuongMocContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductsController(XuongMocContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Admins/Products
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync();

            return View(products);
        }

        // GET: Admins/Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .AsSplitQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            var vm = new ProductManageVM
            {
                Product = product,
                Variants = product.ProductVariants.ToList(),
                Images = product.ProductImages.ToList()
            };

            return View(vm);
        }

        // GET: Admins/Products/Create
        public async Task<IActionResult> Create()
        {
            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name");

            var vm = new ProductManageVM
            {
                Product = new Product { Status = 1, WarrantyMonths = 12, Rating = 5 },
                Variants = new List<ProductVariant> { new ProductVariant { IsDefault = true, OriginalPrice = 0, SalePrice = 0 } }
            };
            return View(vm);
        }

        // POST: Admins/Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(100 * 1024 * 1024)] // Cho phép upload tối đa 100MB ảnh hàng loạt
        public async Task<IActionResult> Create(ProductManageVM vm, IFormFile? primaryImageFile, List<IFormFile>? secondaryImageFiles)
        {
            // 1. DỌN BỎ VALIDATION CHO CÁC ĐỐI TƯỢNG HỆ THỐNG & NAVIGATION
            ModelState.Remove("Product.Category");
            ModelState.Remove("Product.Slug");
            ModelState.Remove("Product.CreatedBy");
            ModelState.Remove("Product.UpdatedBy");
            ModelState.Remove("Product.ProductImages");
            ModelState.Remove("Product.ProductVariants");
            ModelState.Remove("Product.ContactRequests");

            if (vm.Variants != null)
            {
                for (int i = 0; i < vm.Variants.Count; i++)
                {
                    ModelState.Remove($"Variants[{i}].Product");
                    ModelState.Remove($"Variants[{i}].CreatedBy");
                    ModelState.Remove($"Variants[{i}].UpdatedBy");

                    // Validate kiểm tra logic giá (SalePrice <= OriginalPrice) tránh vi phạm CHK_ProductVariant_Price
                    if (vm.Variants[i].SalePrice > vm.Variants[i].OriginalPrice)
                    {
                        ModelState.AddModelError($"Variants[{i}].SalePrice", "Giá bán thực tế không được lớn hơn Giá gốc!");
                    }
                }
            }

            // 2. TỰ ĐỘNG TẠO SLUG NẾU ĐỂ TRỐNG
            if (string.IsNullOrWhiteSpace(vm.Product.Slug) && !string.IsNullOrWhiteSpace(vm.Product.Name))
            {
                vm.Product.Slug = vm.Product.Name.ToLower().Trim().Replace(" ", "-");
            }

            if (ModelState.IsValid)
            {
                // Gán thông tin audit hệ thống
                vm.Product.CreatedAt = DateTime.Now;
                vm.Product.CreatedBy = User.Identity?.Name ?? "Admin";
                vm.Product.ViewCount = 0;

                _context.Products.Add(vm.Product);
                await _context.SaveChangesAsync(); // Lưu để lấy Product.Id

                // Lưu danh sách Biến thể
                if (vm.Variants != null && vm.Variants.Any())
                {
                    foreach (var variant in vm.Variants)
                    {
                        if (!string.IsNullOrWhiteSpace(variant.MaterialName))
                        {
                            variant.ProductId = vm.Product.Id;
                            variant.CreatedAt = DateTime.Now;
                            variant.CreatedBy = User.Identity?.Name ?? "Admin";
                            _context.ProductVariants.Add(variant);
                        }
                    }
                }

                // Upload Hàng loạt Ảnh (Chính & Phụ)
                string uploadDir = Path.Combine(_env.WebRootPath, "content/images/products");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                // Upload Ảnh chính đại diện
                if (primaryImageFile != null && primaryImageFile.Length > 0)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(primaryImageFile.FileName);
                    string filePath = Path.Combine(uploadDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await primaryImageFile.CopyToAsync(stream);
                    }

                    _context.ProductImages.Add(new ProductImage
                    {
                        ProductId = vm.Product.Id,
                        Path = "content/images/products/" + fileName,
                        AltText = vm.Product.Name,
                        IsPrimary = true,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name ?? "Admin"
                    });
                }

                // Upload HÀNG LOẠT Ảnh phụ trong 1 lượt Submit
                if (secondaryImageFiles != null && secondaryImageFiles.Any())
                {
                    foreach (var file in secondaryImageFiles)
                    {
                        if (file.Length > 0)
                        {
                            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                            string filePath = Path.Combine(uploadDir, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            _context.ProductImages.Add(new ProductImage
                            {
                                ProductId = vm.Product.Id,
                                Path = "content/images/products/" + fileName,
                                AltText = vm.Product.Name,
                                IsPrimary = false,
                                CreatedAt = DateTime.Now,
                                CreatedBy = User.Identity?.Name ?? "Admin"
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", vm.Product.CategoryId);
            return View(vm);
        }

        // GET: Admins/Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", product.CategoryId);

            var vm = new ProductManageVM
            {
                Product = product,
                Variants = product.ProductVariants.ToList(),
                Images = product.ProductImages.ToList()
            };

            return View(vm);
        }

        // POST: Admins/Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(100 * 1024 * 1024)] // Cho phép upload tối đa 100MB ảnh hàng loạt khi chỉnh sửa
        public async Task<IActionResult> Edit(int id, ProductManageVM vm, IFormFile? primaryImageFile, List<IFormFile>? secondaryImageFiles)
        {
            if (id != vm.Product.Id) return NotFound();

            ModelState.Remove("Product.Category");
            ModelState.Remove("Product.CreatedBy");
            ModelState.Remove("Product.UpdatedBy");
            ModelState.Remove("Product.ProductImages");
            ModelState.Remove("Product.ProductVariants");
            ModelState.Remove("Product.ContactRequests");

            if (vm.Variants != null)
            {
                for (int i = 0; i < vm.Variants.Count; i++)
                {
                    ModelState.Remove($"Variants[{i}].Product");
                    ModelState.Remove($"Variants[{i}].CreatedBy");
                    ModelState.Remove($"Variants[{i}].UpdatedBy");

                    // Validate kiểm tra logic giá (SalePrice <= OriginalPrice)
                    if (vm.Variants[i].SalePrice > vm.Variants[i].OriginalPrice)
                    {
                        ModelState.AddModelError($"Variants[{i}].SalePrice", "Giá bán thực tế không được lớn hơn Giá gốc!");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Ghi lịch sử Slug nếu người dùng sửa Slug
                    var oldProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                    if (oldProduct != null && oldProduct.Slug != vm.Product.Slug)
                    {
                        _context.SlugHistories.Add(new SlugHistory
                        {
                            EntityType = "Product",
                            EntityId = id,
                            OldSlug = oldProduct.Slug,
                            CreatedAt = DateTime.Now,
                            CreatedBy = User.Identity?.Name ?? "Admin"
                        });
                    }

                    // 1. Cập nhật Product chính
                    vm.Product.UpdatedAt = DateTime.Now;
                    vm.Product.UpdatedBy = User.Identity?.Name ?? "Admin";
                    _context.Update(vm.Product);

                    // 2. Cập nhật lại Biến thể
                    var oldVariants = _context.ProductVariants.Where(v => v.ProductId == id);
                    _context.ProductVariants.RemoveRange(oldVariants);

                    if (vm.Variants != null)
                    {
                        foreach (var variant in vm.Variants)
                        {
                            if (!string.IsNullOrWhiteSpace(variant.MaterialName))
                            {
                                variant.Id = 0;
                                variant.ProductId = id;
                                variant.CreatedAt = DateTime.Now;
                                variant.CreatedBy = User.Identity?.Name ?? "Admin";
                                _context.ProductVariants.Add(variant);
                            }
                        }
                    }

                    // 3. Upload HÀNG LOẠT Ảnh mới khi bấm Cập nhật
                    string uploadDir = Path.Combine(_env.WebRootPath, "content/images/products");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                    // Thay thế Ảnh đại diện chính nếu có chọn file mới
                    if (primaryImageFile != null && primaryImageFile.Length > 0)
                    {
                        var oldPrimaryImages = _context.ProductImages.Where(i => i.ProductId == id && i.IsPrimary);
                        foreach (var oldImg in oldPrimaryImages)
                        {
                            oldImg.IsPrimary = false;
                        }

                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(primaryImageFile.FileName);
                        string filePath = Path.Combine(uploadDir, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await primaryImageFile.CopyToAsync(stream);
                        }

                        _context.ProductImages.Add(new ProductImage
                        {
                            ProductId = id,
                            Path = "content/images/products/" + fileName,
                            AltText = vm.Product.Name,
                            IsPrimary = true,
                            CreatedAt = DateTime.Now,
                            CreatedBy = User.Identity?.Name ?? "Admin"
                        });
                    }

                    // Đẩy HÀNG LOẠT nhiều ảnh phụ mới vào Album trong cùng 1 lần Submit
                    if (secondaryImageFiles != null && secondaryImageFiles.Any())
                    {
                        foreach (var file in secondaryImageFiles)
                        {
                            if (file.Length > 0)
                            {
                                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                                string filePath = Path.Combine(uploadDir, fileName);

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                _context.ProductImages.Add(new ProductImage
                                {
                                    ProductId = id,
                                    Path = "content/images/products/" + fileName,
                                    AltText = vm.Product.Name,
                                    IsPrimary = false,
                                    CreatedAt = DateTime.Now,
                                    CreatedBy = User.Identity?.Name ?? "Admin"
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(vm.Product.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", vm.Product.CategoryId);
            return View(vm);
        }

        // GET: Admins/Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .AsSplitQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            var vm = new ProductManageVM
            {
                Product = product,
                Variants = product.ProductVariants.ToList(),
                Images = product.ProductImages.ToList()
            };

            return View(vm);
        }

        // POST: Admins/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product != null)
            {
                foreach (var img in product.ProductImages)
                {
                    if (!string.IsNullOrEmpty(img.Path))
                    {
                        string filePath = Path.Combine(_env.WebRootPath, img.Path);
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                }

                _context.ProductImages.RemoveRange(product.ProductImages);
                _context.ProductVariants.RemoveRange(product.ProductVariants);
                _context.Products.Remove(product);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}