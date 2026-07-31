Console.WriteLine("Hello, World!");


// Console.WriteLine("enum:" + ImportModeEnum.CreateTableOnly);

var values = new Dictionary<String,String?>(StringComparer.OrdinalIgnoreCase);
values["abc"] = "111";
foreach(var value in values)
{
    Console.WriteLine(value);
}
Console.WriteLine(values);

