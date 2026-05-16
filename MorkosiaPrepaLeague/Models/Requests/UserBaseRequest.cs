using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public abstract class UserBaseRequest
    {
        [MinLength(2)]
        [MaxLength(100)]
        public virtual string? FullName { get; set; }
        
        [MaxLength(50)]
        [RegularExpression(@"^(middleSchool|university|admin)$", ErrorMessage = "Class must be either 'middleSchool', 'university' or 'admin'.")]
        public virtual string? Class { get; set; }
        
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_\-+={}[\]:;""'<>,.?/\\|~`])[a-zA-Z\d!@#$%^&*()_\-+={}[\]:;""'<>,.?/\\|~`]{6,}$", ErrorMessage = "Password must be at least 6 characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public virtual string? Password { get; set; }
    }
}
