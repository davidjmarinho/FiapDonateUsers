namespace FiapDonateUsers.Domain.ValueObjects;

public static class CpfValidator
{
    public static string Normalize(string cpf)
    {
        return new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    public static bool IsValid(string cpf)
    {
        var digits = Normalize(cpf);

        if (digits.Length != 11)
            return false;

        if (digits.Distinct().Count() == 1)
            return false;

        var numbers = digits.Select(c => c - '0').ToArray();

        var firstCheck = CalculateCheckDigit(numbers, 9);
        if (firstCheck != numbers[9])
            return false;

        var secondCheck = CalculateCheckDigit(numbers, 10);
        if (secondCheck != numbers[10])
            return false;

        return true;
    }

    private static int CalculateCheckDigit(int[] numbers, int length)
    {
        var weight = length + 1;
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += numbers[i] * (weight - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
