namespace AsianFoodRestaurant.Models
{
    
    public enum EmployeeRole
    {
        Waiter,
        Chef,
        Cashier
    }

   
    public enum OrderStatus
    {
        Pending,      // just placed by customer
        Accepted,     // admin accepted 
        Declined,     // admin declined
        Preparing,    // an employee claimed it and marked "preparing"
        Completed,    // employee marked "completed" internally
        Delivered,    
        Cancelled
    }

    public enum PaymentMethodType
    {
        Bank,
        Card,
        COD,
        Bkash,
        Nagad
    }

    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Cancelled,
        Refunded 
    }
}
