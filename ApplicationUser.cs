using Microsoft.AspNetCore.Identity;

namespace AsianFoodRestaurant.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }

        // Admin can activate/deactivate a customer account
        public bool IsActive { get; set; } = true;

        
       
        public EmployeeRole? EmployeeRole { get; set; }
    }
}
