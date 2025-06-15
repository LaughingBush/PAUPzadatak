using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using PAUPzadatak.Models;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using Dapper;

namespace PAUPzadatak.Controllers
{
    public class AccountController : Controller
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["BankingConnection"].ConnectionString;

        // GET: Account/Login
        public ActionResult Login()
        {
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var user = connection.QueryFirstOrDefault<User>(
                        "SELECT * FROM Users WHERE Username = @Username AND Password = @Password",
                        new { model.Username, model.Password });

                    if (user != null)
                    {
                        // Update last login
                        connection.Execute(
                            "UPDATE Users SET LastLoginDate = @LastLoginDate WHERE Id = @Id",
                            new { LastLoginDate = DateTime.Now, user.Id });

                        // Set authentication cookie
                        FormsAuthentication.SetAuthCookie(user.Username, model.RememberMe);

                        // Set session variables
                        Session["IsAdmin"] = user.IsAdmin;
                        Session["UserFullName"] = user.FullName;
                        Session["UserId"] = user.Id;
                        Session["AccountNumber"] = user.AccountNumber;
                        Session["Balance"] = user.Balance;

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                            return Redirect(returnUrl);
                        
                        return RedirectToAction("Dashboard", "Banking");
                    }

                    ModelState.AddModelError("", "Invalid username or password.");
                }
            }

            return View(model);
        }

        // GET: Account/Logout
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Login");
        }

        [Authorize]
        public ActionResult UserProfile()
        {
            int userId = (int)Session["UserId"];
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var user = connection.QueryFirstOrDefault<User>(
                    "SELECT * FROM Users WHERE Id = @Id",
                    new { Id = userId });
                return View(user);
            }
        }

        [Authorize]
        public ActionResult UserList()
        {
            if (!IsAdminUser())
                return RedirectToAction("Login");

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var users = connection.Query<User>(
                    "SELECT * FROM Users WHERE IsAdmin = 0").ToList();
                return View(users);
            }
        }

        [Authorize]
        public ActionResult Register()
        {
            if (!IsAdminUser())
                return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User model)
        {
            if (!IsAdminUser())
                return RedirectToAction("Login");

            if (ModelState.IsValid)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var exists = connection.QueryFirstOrDefault<int>(
                        "SELECT 1 FROM Users WHERE Username = @Username",
                        new { model.Username }) != 0;

                    if (exists)
                    {
                        ModelState.AddModelError("", "Username already exists.");
                        return View(model);
                    }

                    // Get the next account number
                    var lastId = connection.QueryFirstOrDefault<int>(
                        "SELECT COALESCE(MAX(Id), 0) FROM Users") + 1;
                    model.AccountNumber = (1000000000 + lastId).ToString();
                    model.IsAdmin = false;
                    model.AccountCreatedDate = DateTime.Now;

                    connection.Execute(@"
                        INSERT INTO Users (Username, Password, FullName, Email, AccountNumber, 
                                        Balance, PhoneNumber, Address, IsAdmin, AccountCreatedDate)
                        VALUES (@Username, @Password, @FullName, @Email, @AccountNumber,
                                @Balance, @PhoneNumber, @Address, @IsAdmin, @AccountCreatedDate)",
                        model);

                    return RedirectToAction("UserList");
                }
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        public ActionResult UpdateProfile(User model)
        {
            if (ModelState.IsValid)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    connection.Execute(@"
                        UPDATE Users 
                        SET FullName = @FullName, Email = @Email, 
                            PhoneNumber = @PhoneNumber, Address = @Address
                        WHERE Id = @Id",
                        model);

                    return RedirectToAction("UserProfile");
                }
            }
            return View("UserProfile", model);
        }

        [Authorize]
        public ActionResult ChangePassword()
        {
            return View();
        }

        protected bool IsAdminUser()
        {
            return Session["IsAdmin"] != null && (bool)Session["IsAdmin"];
        }
    }
} 