using FluentValidation;
using RealTimePoll.Application.DTOs.Auth;
using RealTimePoll.Application.DTOs.Poll;
using RealTimePoll.Application.DTOs.Vote;

namespace RealTimePoll.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad boş olamaz.")
            .MaximumLength(50).WithMessage("Ad en fazla 50 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad boş olamaz.")
            .MaximumLength(50).WithMessage("Soyad en fazla 50 karakter olabilir.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta boş olamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre boş olamaz.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .Matches(@"[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches(@"[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches(@"[0-9]").WithMessage("Şifre en az bir rakam içermelidir.")
            .Matches(@"[!@#$%^&*(),.?""':{}|<>]").WithMessage("Şifre en az bir özel karakter içermelidir.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Şifre onayı boş olamaz.")
            .Equal(x => x.Password).WithMessage("Şifreler eşleşmiyor.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta boş olamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre boş olamaz.");
    }
}

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta boş olamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");
    }
}

public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token geçersiz.");
        RuleFor(x => x.NewPassword)
            .NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").Matches(@"[a-z]")
            .Matches(@"[0-9]").Matches(@"[!@#$%^&*(),.?""':{}|<>]");
        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Şifreler eşleşmiyor.");
    }
}

public class CreatePollValidator : AbstractValidator<CreatePollRequest>
{
    public CreatePollValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Anket başlığı boş olamaz.")
            .MaximumLength(200).WithMessage("Başlık en fazla 200 karakter olabilir.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Açıklama en fazla 1000 karakter olabilir.");

        RuleFor(x => x.StartsAt)
            .NotEmpty().WithMessage("Başlangıç tarihi boş olamaz.")
            .GreaterThanOrEqualTo(DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Başlangıç tarihi geçmişte olamaz.");

        RuleFor(x => x.EndsAt)
            .NotEmpty().WithMessage("Bitiş tarihi boş olamaz.")
            .GreaterThan(x => x.StartsAt).WithMessage("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");

        RuleFor(x => x.Options)
            .NotNull().WithMessage("Anket seçenekleri boş olamaz.")
            .Must(o => o.Count >= 2).WithMessage("En az 2 seçenek girilmelidir.")
            .Must(o => o.Count <= 10).WithMessage("En fazla 10 seçenek girilebilir.");

        RuleForEach(x => x.Options).ChildRules(opt =>
        {
            opt.RuleFor(o => o.Text)
                .NotEmpty().WithMessage("Seçenek metni boş olamaz.")
                .MaximumLength(200).WithMessage("Seçenek metni en fazla 200 karakter olabilir.");
        });
    }
}

public class CastVoteValidator : AbstractValidator<CastVoteRequest>
{
    public CastVoteValidator()
    {
        RuleFor(x => x.PollId).NotEmpty().WithMessage("Anket ID geçersiz.");
        RuleFor(x => x.OptionIds)
            .NotNull().NotEmpty().WithMessage("En az bir seçenek seçmelisiniz.")
            .Must(o => o.Count >= 1).WithMessage("En az bir seçenek seçmelisiniz.");
    }
}
