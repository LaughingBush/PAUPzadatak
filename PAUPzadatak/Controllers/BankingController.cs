using System;
using System.Web.Mvc;
using PAUPzadatak.Models;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using Dapper;

namespace PAUPzadatak.Controllers
{
    [Authorize]
    public class BankingController : Controller
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["BankingConnection"].ConnectionString;

        public ActionResult Dashboard()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            int userId = (int)Session["UserId"];
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var recentTransactions = connection.Query<TransactionViewModel>(
                @"SELECT TOP 5 * FROM Transactions 
                 WHERE UserId = @UserId 
                 ORDER BY TransactionDate DESC", new { UserId = userId }).ToList();

                var user = connection.QueryFirstOrDefault<User>(
                    "SELECT * FROM Users WHERE Id = @Id",
                    new { Id = userId });

                ViewBag.Balance = user.Balance;
                ViewBag.RecentTransactions = recentTransactions;
                return View();
            }
        }

        public ActionResult Transfer()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            return View(new TransactionViewModel { Type = TransactionType.Transfer });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Transfer(TransactionViewModel model, string recipientAccountNumber)
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid && model.Amount > 0)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int senderId = (int)Session["UserId"];
                            var sender = connection.QueryFirstOrDefault<User>(
                                "SELECT * FROM Users WHERE Id = @Id",
                                new { Id = senderId }, transaction);
                            var recipient = connection.QueryFirstOrDefault<User>(
                                "SELECT * FROM Users WHERE AccountNumber = @AccountNumber",
                                new { AccountNumber = recipientAccountNumber }, transaction);

                            if (recipient == null)
                            {
                                ModelState.AddModelError("", "Recipient account not found.");
                                return View(model);
                            }

                            if (sender.Balance < model.Amount)
                            {
                                ModelState.AddModelError("", "Insufficient funds for transfer.");
                                return View(model);
                            }

                            // Update balances
                            connection.Execute(
                                "UPDATE Users SET Balance = Balance - @Amount WHERE Id = @Id",
                                new { Amount = model.Amount, Id = senderId }, transaction);

                            connection.Execute(
                                "UPDATE Users SET Balance = Balance + @Amount WHERE Id = @Id",
                                new { Amount = model.Amount, Id = recipient.Id }, transaction);

                            // Record sender's transaction
                            connection.Execute(@"
                                INSERT INTO Transactions (UserId, Amount, Type, Description, BalanceAfterTransaction)
                                VALUES (@UserId, @Amount, @Type, @Description, @BalanceAfterTransaction)",
                                new
                                {
                                    UserId = senderId,
                                    Amount = model.Amount,
                                    Type = TransactionType.Transfer.ToString(),
                                    Description = $"Transfer to {recipient.AccountNumber} - {model.Description}",
                                    BalanceAfterTransaction = sender.Balance - model.Amount
                                }, transaction);

                            // Record recipient's transaction
                            connection.Execute(@"
                                INSERT INTO Transactions (UserId, Amount, Type, Description, BalanceAfterTransaction)
                                VALUES (@UserId, @Amount, @Type, @Description, @BalanceAfterTransaction)",
                                new
                                {
                                    UserId = recipient.Id,
                                    Amount = model.Amount,
                                    Type = TransactionType.Transfer.ToString(),
                                    Description = $"Transfer from {sender.AccountNumber} - {model.Description}",
                                    BalanceAfterTransaction = recipient.Balance + model.Amount
                                }, transaction);

                            transaction.Commit();
                            Session["Balance"] = sender.Balance - model.Amount;
                            return RedirectToAction("Dashboard");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            return View(model);
        }

        public ActionResult Deposit()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            return View(new TransactionViewModel { Type = TransactionType.Deposit });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Deposit(TransactionViewModel model)
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            int userId = (int)Session["UserId"];
            if ((bool)Session["IsAdmin"] && Request["TargetUserId"] != null && int.TryParse(Request["TargetUserId"], out int adminTargetId))
            {
                userId = adminTargetId;
            }

            if (ModelState.IsValid && model.Amount > 0)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Update balance
                            connection.Execute(
                                "UPDATE Users SET Balance = Balance + @Amount WHERE Id = @Id",
                                new { Amount = model.Amount, Id = userId }, transaction);

                            var newBalance = connection.QueryFirstOrDefault<decimal>(
                                "SELECT Balance FROM Users WHERE Id = @Id",
                                new { Id = userId }, transaction);

                            // Record transaction
                            connection.Execute(@"
                                INSERT INTO Transactions (UserId, Amount, Type, Description, BalanceAfterTransaction)
                                VALUES (@UserId, @Amount, @Type, @Description, @BalanceAfterTransaction)",
                                new
                                {
                                    UserId = userId,
                                    Amount = model.Amount,
                                    Type = TransactionType.Deposit.ToString(),
                                    Description = "Deposit",
                                    BalanceAfterTransaction = newBalance
                                }, transaction);

                            transaction.Commit();
                            if (!(bool)Session["IsAdmin"])
                                Session["Balance"] = newBalance;
                            return RedirectToAction("Dashboard");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            return View(model);
        }

        public ActionResult Withdraw()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            return View(new TransactionViewModel { Type = TransactionType.Withdrawal });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Withdraw(TransactionViewModel model)
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            int userId = (int)Session["UserId"];
            if ((bool)Session["IsAdmin"] && Request["TargetUserId"] != null && int.TryParse(Request["TargetUserId"], out int adminTargetId))
            {
                userId = adminTargetId;
            }

            if (ModelState.IsValid && model.Amount > 0)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var user = connection.QueryFirstOrDefault<User>(
                                "SELECT * FROM Users WHERE Id = @Id",
                                new { Id = userId }, transaction);

                            if (user == null)
                            {
                                ModelState.AddModelError("", "User not found.");
                                return View(model);
                            }

                            if (user.Balance < model.Amount)
                            {
                                ModelState.AddModelError("", "Insufficient funds for withdrawal.");
                                return View(model);
                            }

                            // Update balance
                            connection.Execute(
                                "UPDATE Users SET Balance = Balance - @Amount WHERE Id = @Id",
                                new { Amount = model.Amount, Id = userId }, transaction);

                            var newBalance = user.Balance - model.Amount;

                            // Record transaction
                            connection.Execute(@"
                                INSERT INTO Transactions (UserId, Amount, Type, Description, BalanceAfterTransaction)
                                VALUES (@UserId, @Amount, @Type, @Description, @BalanceAfterTransaction)",
                                new
                                {
                                    UserId = userId,
                                    Amount = model.Amount,
                                    Type = TransactionType.Withdrawal.ToString(),
                                    Description = "Withdrawal",
                                    BalanceAfterTransaction = newBalance
                                }, transaction);

                            transaction.Commit();
                            if (!(bool)Session["IsAdmin"])
                                Session["Balance"] = newBalance;
                            return RedirectToAction("Dashboard");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            return View(model);
        }

        public ActionResult TransactionHistory()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            int userId = (int)Session["UserId"];
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var transactions = connection.Query<TransactionViewModel>(
                    @"SELECT * FROM Transactions 
                      WHERE UserId = @UserId 
                      ORDER BY TransactionDate DESC",
                    new { UserId = userId }).ToList();
                return View(transactions);
            }
        }

        [Authorize(Roles = "Admin")]
        public ActionResult AllTransactions()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var transactions = connection.Query<TransactionViewModel>(
                    @"SELECT t.*, u.Username, u.AccountNumber 
                      FROM Transactions t
                      JOIN Users u ON t.UserId = u.Id
                      ORDER BY TransactionDate DESC").ToList();
                return View(transactions);
            }
        }
    }
} 