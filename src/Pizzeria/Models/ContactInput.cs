using System.ComponentModel.DataAnnotations;

namespace Pizzeria.Models;

/// <summary>
/// Input model of the contact form. The view binds this type, never the entity.
/// Maximum lengths match the ContactMessage columns (80, 20, 254, 1000).
/// </summary>
public class ContactInput : IValidatableObject
{
    private const string PhoneError = "Escribe un teléfono válido: de 7 a 20 caracteres, solo números y espacios.";

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "Escribe tu nombre.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 80 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Teléfono")]
    [StringLength(20, ErrorMessage = PhoneError)]
    // Optional leading "+", then at least 7 real digits; spaces may go between or after them.
    // The total length (20) is enforced by StringLength.
    [RegularExpression(@"^\+?(?: *[0-9]){7,} *$", ErrorMessage = PhoneError)]
    public string? Phone { get; set; }

    [Display(Name = "Correo")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(254, ErrorMessage = "El correo no puede superar los 254 caracteres.")]
    public string? Email { get; set; }

    [Display(Name = "Mensaje")]
    [Required(ErrorMessage = "Escribe tu mensaje.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "El mensaje debe tener entre 5 y 1000 caracteres.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Honeypot: hidden from people, never validated and never stored.</summary>
    public string? Website { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Phone) && string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult(
                "Indica al menos un teléfono o un correo para poder responderte.",
                [nameof(Phone), nameof(Email)]);
        }
    }
}
