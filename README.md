# LePack

Just some stupid codes to deal with files in some stupid games.

My top favorite games. Some are new, some are old, even dead games.

## Installation

My lazy ass will never make a NuGet package for this shit.

## Usage

Similar to how you deal with ZipFile class.

```csharp
using LePack.VFS;
using System;

class Program
{
    static void Main(string[] args)
    {
        string startPath = @".\start";
        string packedPath = @".\result.pac";
        string extractPath = @".\extract";

        PackFile.CreateFromDirectory(startPath, packedPath);

        PackFile.ExtractToDirectory(packedPath, extractPath);
    }
}
```

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License
[MIT](https://choosealicense.com/licenses/mit/)
