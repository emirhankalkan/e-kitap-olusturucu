using System.ComponentModel.DataAnnotations;

namespace EKitap.Api.Contracts;

public sealed class CreateBookRequest
{
    [Required(ErrorMessage = "Kitap adı zorunludur.")]
    [StringLength(200, ErrorMessage = "Kitap adı en fazla 200 karakter olabilir.")]
    public string Name { get; init; } = string.Empty;

    public List<IFormFile> Files { get; init; } = [];
}
