# PeaPdf
PeaPdf is a .NET library to fill in PDF form fields.

If you would like another feature for working with PDFs, please provide feedback on [GitHub](https://github.com/ecymerman/peapdf/issues), or email me at elliott@seapeayou.net .

## License
The license is Apache License, Version 2.0, allowing PeaPdf to be used for free, including commercially.

All 3rd-party components used have a likewise permissive license, see `LICENSE-3RD-PARTY` for details.

## Installation
Install NuGet package SeaPeaYou.PeaPdf.

## Usage
```C#
var pdf = new PDF(File.ReadAllBytes(@"C:\test.pdf"));
pdf.SetTextField("First Name", "Julian");
pdf.FlattenFields();
var savePath = @"C:\final.pdf";
File.WriteAllBytes(savePath, pdf.Save());
```

## Dependencies
PeaPdf's framework is .NET Standard 2.0, allowing it to be used with .NET Framework & .NET Core.

