using System;
using System.Collections.Generic;

namespace PGH_DevXuongMoc_2026.Models;

public partial class ContentBlock
{
    public int Id { get; set; }

    public byte Group { get; set; }

    public string Key { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
