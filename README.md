# Asian Food Restaurant — ASP.NET Core MVC Project

A food ordering website with three actors: **Admin**, **Customer**, **Employee** (Waiter/Chef/Cashier).
Built with ASP.NET Core 8 MVC, EF Core, ASP.NET Core Identity, SQL Server 2022, and SSLCommerz Sandbox for payments.

## 1. Open in Visual Studio
1. Copy this whole folder somewhere on your machine.
2. Open Visual Studio 2022 → **Open a local folder** (or create a blank ASP.NET Core MVC project and copy these files in, overwriting the defaults).
3. Right-click the project → **Restore NuGet Packages** (or it will restore automatically on build).

## 2. Configure the database
Edit `appsettings.json` → `ConnectionStrings:DefaultConnection` to match your SQL Server 2022 instance, e.g.:
```
Server=localhost\SQLEXPRESS;Database=AsianFoodRestaurantDb;Trusted_Connection=True;TrustServerCertificate=True;
```

Then, in **Package Manager Console**:
```
Add-Migration InitialCreate
Update-Database
```
This creates the database, Identity tables, and app tables (MenuItems, Orders, OrderItems, Payments).

Running the app the first time will also **auto-seed**:
- Roles: `Admin`, `Employee`, `Customer`
- A default Admin login: **admin@asianfood.com / Admin@12345** — change this password after first login (see `Data/DbInitializer.cs`).

## 3. Set up SSLCommerz Sandbox
1. Register a free sandbox merchant account: https://developer.sslcommerz.com/registration/
2. Copy your **Store ID** and **Store Password** into `appsettings.json` under `SslCommerz`.
3. Test cards/wallets for the sandbox are listed in SSLCommerz's docs — use those, not real payment details.
4. If `Pay Now` fails with a message about credentials, it means the Store ID/Password aren't set yet.

## 4. How the workflow matches your requirements

**Admin**
- Assigns Employee role (Waiter / Chef / Cashier) when creating an employee account — *Admin → Employees → Add Employee*.
- Full menu CRUD with image upload — *Admin → Manage Menu*.
- Stock management (quantity in/out, "Stock Out" badge) — *Admin → Stock*.
- Activate/Deactivate customer accounts — *Admin → Customers*.
- Accept / Decline / Delete orders — *Admin → Orders*.

**Customer**
- Registers/logs in, browses menu, adds to cart.
- Cart has +/− quantity buttons; − down to 0 removes the item; stock is checked on every change and shows "Stock out, you cannot order this food" when exceeded.
- Checkout → choose Delivery Address + Payment Method (Bank / Card / COD / Bkash / Nagad) → Confirm.
  - COD confirms immediately.
  - Bank/Card/Bkash/Nagad redirect to the **SSLCommerz Sandbox** gateway page.
- My Orders dashboard shows live status: Pending → Accepted → Preparing → Delivered, plus an **Invoice** link once paid.

**Employee**
- Dashboard shows orders the Admin has **Accepted** and nobody has claimed yet.
- Claiming moves the order into **My Orders**.
- "Mark as Preparing" → customer sees **Preparing**.
- "Mark as Completed" → customer sees **Delivered**.

## 5. Notes / things to double check before submitting
- I used a session-based cart (no DB table) — simplest for a practicum project; it clears if the browser session ends.
- Public self-registration only creates Customer accounts (security best practice — you don't want random signups becoming Admin). Admin/Employee accounts are created from the Admin panel. Mention this design decision in your report.
- Uploaded menu images are saved to `wwwroot/uploads/menu/`.
- Bootstrap 5 is loaded via CDN in `_Layout.cshtml` — no `npm`/`libman` setup needed, but you do need an internet connection when running the site (or download Bootstrap locally and update the `<link>`/`<script>` tags).
- This is a solid working skeleton covering every flow you listed. Feel free to polish the CSS/branding, and let me know if you want additional screens (e.g., order search/filter, sales reports, email notifications) — I can add those too.
