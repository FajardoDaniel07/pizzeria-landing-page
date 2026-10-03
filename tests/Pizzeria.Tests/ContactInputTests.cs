using System.ComponentModel.DataAnnotations;
using Pizzeria.Models;

namespace Pizzeria.Tests;

/// <summary>
/// Server-side validation of the contact form model (RF-18, RNF-02).
/// Message texts are the literal ones fixed in RF-18 (assumption S-1).
/// </summary>
public class ContactInputTests
{
    private const string NameRequired = "Escribe tu nombre.";
    private const string NameLength = "El nombre debe tener entre 2 y 80 caracteres.";
    private const string PhoneInvalid = "Escribe un teléfono válido: de 7 a 20 caracteres, solo números y espacios.";
    private const string EmailInvalid = "Escribe un correo válido.";
    private const string EmailLength = "El correo no puede superar los 254 caracteres.";
    private const string MessageRequired = "Escribe tu mensaje.";
    private const string MessageLength = "El mensaje debe tener entre 5 y 1000 caracteres.";
    private const string ContactRequired = "Indica al menos un teléfono o un correo para poder responderte.";

    private static ContactInput Build(
        string name = "Ana",
        string? phone = "311 370 6576",
        string? email = null,
        string message = "Quiero reservar",
        string? website = null) =>
        new()
        {
            Name = name,
            Phone = phone,
            Email = email,
            Message = message,
            Website = website,
        };

    private static List<ValidationResult> Validate(ContactInput input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
        return results;
    }

    private static void AssertValid(ContactInput input)
    {
        var results = Validate(input);
        Assert.True(results.Count == 0, "Unexpected errors: " + string.Join(" | ", results.Select(r => r.ErrorMessage)));
    }

    private static void AssertError(ContactInput input, string expectedMessage, string? expectedMember = null)
    {
        var results = Validate(input);
        var match = results.FirstOrDefault(r => r.ErrorMessage == expectedMessage);

        Assert.True(
            match is not null,
            $"Expected \"{expectedMessage}\" but got: " + string.Join(" | ", results.Select(r => r.ErrorMessage)));

        if (expectedMember is not null)
        {
            Assert.Contains(expectedMember, match!.MemberNames);
        }
    }

    // ---------- Valid inputs (RF-17 data, RF-18 boundary values) ----------

    // RF-17: name + phone, empty email.
    [Fact]
    public void Valid_WithPhoneOnly_RF17()
    {
        AssertValid(Build(phone: "311 370 6576", email: null));
    }

    // RF-17: name + email, empty phone.
    [Fact]
    public void Valid_WithEmailOnly_RF17()
    {
        AssertValid(Build(phone: null, email: "ana@example.com"));
    }

    // RF-17: both phone and email filled in.
    [Fact]
    public void Valid_WithPhoneAndEmail_RF17()
    {
        AssertValid(Build(phone: "311 370 6576", email: "ana@example.com"));
    }

    // RF-18: an empty phone counts as "not provided", not as an invalid phone.
    // (Model binding turns empty form values into null, so an empty Email never reaches the model.)
    [Fact]
    public void Valid_WhenPhoneIsEmptyString_RF18()
    {
        AssertValid(Build(phone: "", email: "ana@example.com"));
    }

    // RF-18: boundary values for Name (2 and 80 characters).
    [Theory]
    [InlineData(2)]
    [InlineData(80)]
    public void Valid_NameAtLengthLimits_RF18(int length)
    {
        AssertValid(Build(name: new string('a', length)));
    }

    // RF-18: boundary values for Message (5 and 1000 characters).
    [Theory]
    [InlineData(5)]
    [InlineData(1000)]
    public void Valid_MessageAtLengthLimits_RF18(int length)
    {
        AssertValid(Build(message: new string('a', length)));
    }

    // RF-18: valid phone formats, including the 7 and 20 character limits.
    [Theory]
    [InlineData("3113706576")]
    [InlineData("+57 311 370 6576")]
    [InlineData("311 370 6576")]
    [InlineData("1234567")]
    [InlineData("12345678901234567890")]
    public void Valid_PhoneFormats_RF18(string phone)
    {
        AssertValid(Build(phone: phone));
    }

    // RF-18 / RNF-02: an email of exactly 254 characters is accepted.
    [Fact]
    public void Valid_EmailWith254Characters_RF18()
    {
        var email = new string('a', 254 - "@example.com".Length) + "@example.com";
        Assert.Equal(254, email.Length);

        AssertValid(Build(phone: null, email: email));
    }

    // RNF-04 / S-5: the honeypot field has no validation of its own.
    [Fact]
    public void Valid_WhenWebsiteHasValue_RNF04()
    {
        AssertValid(Build(website: "https://spam.example"));
    }

    // ---------- Name ----------

    // RF-18: empty or whitespace-only name (S-4).
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Invalid_NameEmptyOrWhitespace_RF18(string name)
    {
        AssertError(Build(name: name), NameRequired, nameof(ContactInput.Name));
    }

    // RF-18: name with 1 character or more than 80.
    [Theory]
    [InlineData(1)]
    [InlineData(81)]
    public void Invalid_NameOutsideLengthLimits_RF18(int length)
    {
        AssertError(Build(name: new string('a', length)), NameLength, nameof(ContactInput.Name));
    }

    // ---------- Phone ----------

    // RF-18: phone that does not match ^\+?[0-9 ]{7,20}$ or is longer than 20 characters.
    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("311-370-6576")]
    [InlineData("123456")]
    [InlineData("123456789012345678901")]
    [InlineData("311 370 6576 ext 2")]
    [InlineData("57+3113706576")]
    public void Invalid_Phone_RF18(string phone)
    {
        AssertError(Build(phone: phone), PhoneInvalid, nameof(ContactInput.Phone));
    }

    // RF-18: an invalid phone is rejected even when a valid email is present.
    [Fact]
    public void Invalid_PhoneIsRejectedEvenWithValidEmail_RF18()
    {
        AssertError(Build(phone: "abc", email: "ana@example.com"), PhoneInvalid, nameof(ContactInput.Phone));
    }

    // ---------- Email ----------

    // RF-18: email with invalid format.
    [Theory]
    [InlineData("ana@")]
    [InlineData("ana")]
    [InlineData("@example.com")]
    public void Invalid_EmailFormat_RF18(string email)
    {
        AssertError(Build(phone: null, email: email), EmailInvalid, nameof(ContactInput.Email));
    }

    // RF-18: email longer than 254 characters.
    [Fact]
    public void Invalid_EmailLongerThan254_RF18()
    {
        var email = new string('a', 255 - "@example.com".Length) + "@example.com";
        Assert.Equal(255, email.Length);

        AssertError(Build(phone: null, email: email), EmailLength, nameof(ContactInput.Email));
    }

    // ---------- Message ----------

    // RF-18: empty or whitespace-only message (S-4).
    [Theory]
    [InlineData("")]
    [InlineData("      ")]
    public void Invalid_MessageEmptyOrWhitespace_RF18(string message)
    {
        AssertError(Build(message: message), MessageRequired, nameof(ContactInput.Message));
    }

    // RF-18: message with fewer than 5 or more than 1000 characters.
    [Theory]
    [InlineData(4)]
    [InlineData(1001)]
    public void Invalid_MessageOutsideLengthLimits_RF18(int length)
    {
        AssertError(Build(message: new string('a', length)), MessageLength, nameof(ContactInput.Message));
    }

    // ---------- At least phone or email ----------

    // RF-18: phone and email both missing (null or empty).
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Invalid_WhenPhoneAndEmailAreBothEmpty_RF18(string? phone, string? email)
    {
        AssertError(Build(phone: phone, email: email), ContactRequired);
    }

    // RF-18: the "at least one" rule lives in IValidatableObject.
    [Fact]
    public void ContactInput_ImplementsIValidatableObject_RF18()
    {
        IValidatableObject validatable = Assert.IsAssignableFrom<IValidatableObject>(Build(phone: null, email: null));

        var input = (ContactInput)validatable;
        var results = validatable.Validate(new ValidationContext(input)).ToList();

        Assert.Contains(results, r => r.ErrorMessage == ContactRequired);
    }

    // RF-18 (negative of the rule): with one contact value the rule reports nothing.
    [Theory]
    [InlineData("3113706576", null)]
    [InlineData(null, "ana@example.com")]
    public void ValidatableObject_ReportsNothing_WhenOneContactValueIsPresent_RF18(string? phone, string? email)
    {
        var input = Build(phone: phone, email: email);

        var results = ((IValidatableObject)input).Validate(new ValidationContext(input)).ToList();

        Assert.Empty(results);
    }

    // ---------- Several errors at once ----------

    // RF-18: every invalid field reports its own message.
    [Fact]
    public void Invalid_ReportsOneErrorPerInvalidField_RF18()
    {
        var results = Validate(Build(name: "a", phone: "abc", email: "ana@", message: "hey"));
        var messages = results.Select(r => r.ErrorMessage).ToList();

        Assert.Contains(NameLength, messages);
        Assert.Contains(PhoneInvalid, messages);
        Assert.Contains(EmailInvalid, messages);
        Assert.Contains(MessageLength, messages);
    }

    // ---------- Lengths aligned with the columns ----------

    // RNF-02: ContactInput maximum lengths match the column lengths (80, 20, 254, 1000).
    [Theory]
    [InlineData(nameof(ContactInput.Name), 80)]
    [InlineData(nameof(ContactInput.Phone), 20)]
    [InlineData(nameof(ContactInput.Email), 254)]
    [InlineData(nameof(ContactInput.Message), 1000)]
    public void StringLength_MatchesColumnLength_RNF02(string propertyName, int expectedMaxLength)
    {
        var property = typeof(ContactInput).GetProperty(propertyName);
        Assert.NotNull(property);

        var attribute = property!
            .GetCustomAttributes(typeof(StringLengthAttribute), inherit: true)
            .Cast<StringLengthAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(expectedMaxLength, attribute!.MaximumLength);
    }
}
