using BCrypt.Net;

Console.Write("Enter password to hash: ");
var password = Console.ReadLine();
var hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

Console.WriteLine(hash);
Console.ReadLine();