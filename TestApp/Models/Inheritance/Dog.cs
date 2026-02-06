namespace TestApp.Models.Inheritance;

public class Dog : Animal
{
    public string Breed { get; set; } = string.Empty;

    public bool IsGoodBoy { get; set; } = true;
}
