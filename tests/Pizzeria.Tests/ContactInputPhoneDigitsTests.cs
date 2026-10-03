using System.ComponentModel.DataAnnotations;
using Pizzeria.Models;

namespace Pizzeria.Tests;

/// <summary>
/// Verification defect D-3: a phone needs at least 7 real digits, not just 7 characters
/// made of digits and spaces (RF-18).
/// </summary>
public class ContactInputPhoneDigitsTests
{
    private const string PhoneInvalid = "Escribe un teléfono válido: de 7 a 20 caracteres, solo números y espacios.";

    private static List<ValidationResult> Validate(string? phone, string? email = null)
    {
        var input = new ContactInput
        {
            Name = "Ana",
            Phone = phone,
            Email = email,
            Message = "Quiero reservar",
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
        return results;
    }

    // D-3: values that reach 7 characters only thanks to spaces are rejected.
    [Theory]
    [InlineData("1      ")]
    [InlineData("+       ")]
    [InlineData("+1     ")]
    [InlineData("1 2 3 4")]
    [InlineData("123 456")]
    [InlineData("+123456")]
    [InlineData("+57 311")]
    [InlineData("1 2 3 4 5 6")]
    [InlineData("      1")]
    public void Invalid_PhoneWithFewerThanSevenDigits_D3(string phone)
    {
        var results = Validate(phone);

        Assert.Contains(
            results,
            r => r.ErrorMessage == PhoneInvalid && r.MemberNames.Contains(nameof(ContactInput.Phone)));
    }

    // D-3: such a value does not count as a way to reply either, even with a valid email present.
    [Fact]
    public void Invalid_PhoneWithFewerThanSevenDigits_IsRejectedEvenWithValidEmail_D3()
    {
        var results = Validate("1      ", email: "ana@example.com");

        Assert.Contains(results, r => r.ErrorMessage == PhoneInvalid);
    }

    // D-3: 7 or more digits stay valid with a leading "+" and spaces between groups.
    [Theory]
    [InlineData("1234567")]
    [InlineData("+1234567")]
    [InlineData("1 2 3 4 5 6 7")]
    [InlineData("123 4567")]
    [InlineData("+57 311 370 6576")]
    [InlineData("+ 57 3113706576")]
    [InlineData("311 370 6576 ")]
    [InlineData("12345678901234567890")]
    [InlineData("+1234567890123456789")]
    public void Valid_PhoneWithAtLeastSevenDigits_D3(string phone)
    {
        var results = Validate(phone);

        Assert.True(results.Count == 0, "Unexpected errors: " + string.Join(" | ", results.Select(r => r.ErrorMessage)));
    }

    // D-3: the maximum length of 20 characters still applies, with the same message.
    [Theory]
    [InlineData("+12345678901234567890")]
    [InlineData("1234567 8901234567890")]
    [InlineData("123456789012345678901")]
    public void Invalid_PhoneLongerThanTwentyCharacters_D3(string phone)
    {
        var results = Validate(phone);

        Assert.Contains(
            results,
            r => r.ErrorMessage == PhoneInvalid && r.MemberNames.Contains(nameof(ContactInput.Phone)));
    }

    // D-3: only digits, spaces and one leading "+" are allowed.
    [Theory]
    [InlineData("++1234567")]
    [InlineData("1234567+")]
    [InlineData("123\t4567")]
    [InlineData("(311) 370 6576")]
    public void Invalid_PhoneWithOtherCharacters_D3(string phone)
    {
        var results = Validate(phone);

        Assert.Contains(results, r => r.ErrorMessage == PhoneInvalid);
    }
}
