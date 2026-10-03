# CustomColorPicker.dll

Third-party WPF drop-down color picker used by `MVVM/Views/SettingsWindow.xaml`
(font color, receiver color, highlight rule color).

| | |
| --- | --- |
| Assembly | `CustomColorPicker, Version=1.0.6880.35456`, not strong-named |
| Namespace | `DropDownCustomColorPicker` (`CustomColorPicker`, `ColorPicker`, `CustomColors`, `ColorToSolidColorBrushConverter`) |
| Target | .NET Framework 4.7, references only WPF and BCL assemblies |
| Metadata | `AssemblyTitle = "WindowsApplication1"`, `Copyright @ 2006` — a sample project compiled locally (build number 6880 ≈ November 2018) |
| Source | No NuGet package with this assembly exists; the namespace matches the CodeProject "Drop Down Custom Color Picker" WPF sample |

The binary is kept in the repository on purpose: the project cannot be built without it, and there is
no package to restore it from.

**Open question:** the license of the original sample is not recorded. If it is CPOL (the CodeProject
default), it is not GPL-3.0-compatible. Before a public release either confirm the license with the
author, or replace the control (for example with `MaterialDesignThemes.Wpf.ColorPicker`, which is
already referenced, or with a small in-house picker).
