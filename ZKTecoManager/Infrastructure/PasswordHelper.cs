using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Npgsql;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// Password policy settings.
    /// </summary>
    public class PasswordPolicy
    {
        public int MinLength { get; set; } = 6;
        public bool RequireUppercase { get; set; } = false;
        public bool RequireLowercase { get; set; } = false;
        public bool RequireNumbers { get; set; } = false;
        public bool RequireSpecial { get; set; } = false;

        public static PasswordPolicy Default => new PasswordPolicy();

        public static PasswordPolicy LoadFromDatabase()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"SELECT min_length, require_uppercase, require_lowercase, require_numbers, require_special
                                FROM password_policy_settings LIMIT 1";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new PasswordPolicy
                            {
                                MinLength = reader.GetInt32(0),
                                RequireUppercase = reader.GetBoolean(1),
                                RequireLowercase = reader.GetBoolean(2),
                                RequireNumbers = reader.GetBoolean(3),
                                RequireSpecial = reader.GetBoolean(4)
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading password policy: {ex.Message}");
            }
            return Default;
        }

        public void SaveToDatabase()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"
                        INSERT INTO password_policy_settings (setting_id, min_length, require_uppercase, require_lowercase, require_numbers, require_special)
                        VALUES (1, @minLength, @upper, @lower, @numbers, @special)
                        ON CONFLICT (setting_id) DO UPDATE SET
                            min_length = @minLength,
                            require_uppercase = @upper,
                            require_lowercase = @lower,
                            require_numbers = @numbers,
                            require_special = @special";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("minLength", MinLength);
                        cmd.Parameters.AddWithValue("upper", RequireUppercase);
                        cmd.Parameters.AddWithValue("lower", RequireLowercase);
                        cmd.Parameters.AddWithValue("numbers", RequireNumbers);
                        cmd.Parameters.AddWithValue("special", RequireSpecial);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving password policy: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Result of password validation.
    /// </summary>
    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public string GetErrorMessage()
        {
            return string.Join("\n", Errors);
        }
    }

    /// <summary>
    /// Provides secure password hashing and verification using PBKDF2.
    /// </summary>
    public static class PasswordHelper
    {
        private const int SaltSize = 16; // 128 bit
        private const int HashSize = 32; // 256 bit
        private const int Iterations = 10000;

        /// <summary>
        /// Hashes a password using PBKDF2 with a random salt.
        /// </summary>
        /// <param name="password">The password to hash</param>
        /// <returns>Base64 encoded string containing salt and hash</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            // Generate a random salt
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash the password with the salt
            byte[] hash;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                hash = pbkdf2.GetBytes(HashSize);
            }

            // Combine salt and hash
            byte[] hashBytes = new byte[SaltSize + HashSize];
            Array.Copy(salt, 0, hashBytes, 0, SaltSize);
            Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verifies a password against a stored hash.
        /// </summary>
        /// <param name="password">The password to verify</param>
        /// <param name="storedHash">The stored hash to verify against</param>
        /// <returns>True if password matches, false otherwise</returns>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            try
            {
                // Check if this is a legacy plaintext password (not base64 encoded hash)
                // Legacy passwords won't be valid base64 of the expected length
                byte[] hashBytes;
                try
                {
                    hashBytes = Convert.FromBase64String(storedHash);
                    if (hashBytes.Length != SaltSize + HashSize)
                    {
                        // This is a legacy plaintext password, do direct comparison
                        return password == storedHash;
                    }
                }
                catch (FormatException)
                {
                    // Not a valid base64 string, treat as legacy plaintext password
                    return password == storedHash;
                }

                // Extract salt from stored hash
                byte[] salt = new byte[SaltSize];
                Array.Copy(hashBytes, 0, salt, 0, SaltSize);

                // Hash the input password with the extracted salt
                byte[] hash;
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    hash = pbkdf2.GetBytes(HashSize);
                }

                // Compare the computed hash with the stored hash
                for (int i = 0; i < HashSize; i++)
                {
                    if (hashBytes[i + SaltSize] != hash[i])
                        return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a password hash is in the new secure format.
        /// </summary>
        /// <param name="storedHash">The stored hash to check</param>
        /// <returns>True if hash is in new format, false if legacy plaintext</returns>
        public static bool IsSecureHash(string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
                return false;

            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);
                return hashBytes.Length == SaltSize + HashSize;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Validates a password against the configured policy.
        /// </summary>
        /// <param name="password">The password to validate</param>
        /// <param name="policy">Optional policy to use. If null, loads from database.</param>
        /// <returns>Validation result with errors if any</returns>
        public static PasswordValidationResult ValidatePassword(string password, PasswordPolicy policy = null)
        {
            var result = new PasswordValidationResult { IsValid = true };

            if (policy == null)
            {
                policy = PasswordPolicy.LoadFromDatabase();
            }

            if (string.IsNullOrEmpty(password))
            {
                result.IsValid = false;
                result.Errors.Add("كلمة المرور مطلوبة");
                return result;
            }

            // Check minimum length
            if (password.Length < policy.MinLength)
            {
                result.IsValid = false;
                result.Errors.Add($"يجب أن تكون كلمة المرور {policy.MinLength} أحرف على الأقل");
            }

            // Check for uppercase
            if (policy.RequireUppercase && !password.Any(char.IsUpper))
            {
                result.IsValid = false;
                result.Errors.Add("يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل (A-Z)");
            }

            // Check for lowercase
            if (policy.RequireLowercase && !password.Any(char.IsLower))
            {
                result.IsValid = false;
                result.Errors.Add("يجب أن تحتوي كلمة المرور على حرف صغير واحد على الأقل (a-z)");
            }

            // Check for numbers
            if (policy.RequireNumbers && !password.Any(char.IsDigit))
            {
                result.IsValid = false;
                result.Errors.Add("يجب أن تحتوي كلمة المرور على رقم واحد على الأقل (0-9)");
            }

            // Check for special characters
            if (policy.RequireSpecial && !password.Any(c => !char.IsLetterOrDigit(c)))
            {
                result.IsValid = false;
                result.Errors.Add("يجب أن تحتوي كلمة المرور على رمز خاص واحد على الأقل (!@#$%^&*)");
            }

            return result;
        }
    }
}
