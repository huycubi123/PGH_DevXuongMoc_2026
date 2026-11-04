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
            _env = env; // Lưu trữ thông tin về môi trường web, bao gồm đường dẫn đến thư mục wwwroot
        }

        // GET: Admins/Products
        public async Task<IActionResult> Index()
        {
            var xuongMocContext = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages);
            return View(await xuongMocContext.ToListAsync());
        }

        // GET: Admins/Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
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
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            var vm = new ProductManageVM
            {
                Variants = new List<ProductVariant> { new ProductVariant { IsDefault = true } }
            };
            return View(vm);
        }

        // POST: Admins/Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductManageVM vm)
        {
            if (ModelState.IsValid)
            {
                // 1. Lưu sản phẩm chính
                vm.Product.CreatedAt = DateTime.Now;
                vm.Product.CreatedBy = User.Identity?.Name ?? "Admin";
                _context.Products.Add(vm.Product);
                await _context.SaveChangesAsync();

                // 2. Lưu các biến thể (ProductVariant)
                if (vm.Variants != null && vm.Variants.Any())
                {
                    foreach (var variant in vm.Variants)
                    {
                        if (!string.IsNullOrEmpty(variant.MaterialName))
                        {
                            variant.ProductId = vm.Product.Id;
                            variant.CreatedAt = DateTime.Now;
                            variant.CreatedBy = User.Identity?.Name ?? "Admin";
                            _context.ProductVariants.Add(variant);
                        }
                    }
                }

                // 3. Xử lý Upload Ảnh
                string uploadDir = Path.Combine(_env.WebRootPath, "content/images/products");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                // Upload ảnh chính
                if (vm.PrimaryImageFile != null && vm.PrimaryImageFile.Length > 0)
                {
                    // Tạo tên file duy nhất để tránh trùng lặp
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.PrimaryImageFile.FileName); // Lấy phần mở rộng của file gốc
                    string filePath = Path.Combine(uploadDir, fileName);    // Tạo đường dẫn đầy đủ để lưu file
                    using (var stream = new FileStream(filePath, FileMode.Create)) // Mở file để ghi dữ liệu
                    {
                        await vm.PrimaryImageFile.CopyToAsync(stream); // Sao chép dữ liệu từ file tải lên vào file trên server
                    }

                    // Lưu thông tin ảnh vào cơ sở dữ liệu
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

                // Upload các ảnh phụ
                if (vm.SecondaryImageFiles != null && vm.SecondaryImageFiles.Any())
                {
                    // đuyệt từng file ảnh phụ được tải lên
                    foreach (var file in vm.SecondaryImageFiles)
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
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", vm.Product.CategoryId);
            return View(vm);
        }

        // GET: Admins/Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);

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
        public async Task<IActionResult> Edit(int id, ProductManageVM vm)
        {
            if (id != vm.Product.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Lấy slug cũ từ CSDL để kiểm tra đổi SlugHistory
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

                    // 2. Cập nhật Biến thể
                    var oldVariants = _context.ProductVariants.Where(v => v.ProductId == id);
                    _context.ProductVariants.RemoveRange(oldVariants);

                    if (vm.Variants != null)
                    {
                        foreach (var variant in vm.Variants)
                        {
                            if (!string.IsNullOrEmpty(variant.MaterialName))
                            {
                                variant.Id = 0;
                                variant.ProductId = id;
                                variant.CreatedAt = DateTime.Now;
                                variant.CreatedBy = User.Identity?.Name ?? "Admin";
                                _context.ProductVariants.Add(variant);
                            }
                        }
                    }

                    // 3. Upload thêm ảnh mới nếu chọn
                    string uploadDir = Path.Combine(_env.WebRootPath, "content/images/products");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                    if (vm.PrimaryImageFile != null && vm.PrimaryImageFile.Length > 0)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.PrimaryImageFile.FileName);
                        string filePath = Path.Combine(uploadDir, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await vm.PrimaryImageFile.CopyToAsync(stream);
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

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(vm.Product.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", vm.Product.CategoryId);
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
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product != null)
            {
                // Xóa file ảnh vật lý
                foreach (var img in product.ProductImages)
                {
                    string filePath = Path.Combine(_env.WebRootPath, img.Path);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
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