from pathlib import Path
import shutil

root = Path(__file__).resolve().parent
project = Path.cwd()
main_path = project / 'src/XSphere/MainWindow.cs'
csproj_path = project / 'src/XSphere/XSphere.csproj'
if not main_path.exists() or not csproj_path.exists():
    raise SystemExit('Run this from the XSphere project root (containing src/XSphere).')

code = main_path.read_text()
csproj = csproj_path.read_text()
if 'private static Control BuildAvatarVisual(' in code:
    raise SystemExit('Image avatar support is already installed.')

start = code.find('    private void ShowProfileSelection()\n')
end = code.find('    private void RebuildShell()\n',start)
if start < 0 or end < 0:
    raise SystemExit('Profile selector was not found; no changes made.')
section = code[start:end]

old_array = '''        string[] avatars =
        {
            "🎮", "👾", "🤖", "👑",
            "🐉", "🦊", "🐺", "🐱",
            "🚀", "⚡", "🔥", "💎",
            "🎯", "🛡️", "👻", "🕹️"
        };'''
new_array = '''        string[] avatars =
        {
            "A01", "A02", "A03", "A04",
            "A05", "A06", "A07", "A08",
            "A09", "A10", "A11", "A12",
            "A13", "A14", "A15", "A16"
        };'''
old_button = '''            var button = MakeButton(
                choice, CardBrush, MainTextBrush);

            button.Width = 65;
            button.Height = 65;
            button.FontSize = 27;
            button.Margin = new Thickness(5);'''
new_button = '''            var button = MakeButton(
                "", CardBrush, MainTextBrush);

            button.Content = BuildAvatarVisual(choice, 72);
            button.Width = 86;
            button.Height = 86;
            button.Padding = new Thickness(4);
            button.Margin = new Thickness(5);
            ToolTip.SetTip(button, choice);'''
old_profile = '''            var select = MakeButton(
                profile.Avatar + "  " + profile.Name,
                CardBrush,
                MainTextBrush);'''
new_profile = '''            var select = MakeButton(
                "",
                CardBrush,
                MainTextBrush);

            var profileVisual = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            profileVisual.Children.Add(BuildAvatarVisual(profile.Avatar, 54));
            profileVisual.Children.Add(Text(profile.Name, 17, MainTextBrush, true));
            select.Content = profileVisual;'''

replacements = [(old_array,new_array,'avatar array'),(old_button,new_button,'avatar buttons'),(old_profile,new_profile,'profile cards')]
for old,new,label in replacements:
    if section.count(old)!=1:
        raise SystemExit(f'Could not locate {label}; no changes made. Send your current ShowProfileSelection() code for a tailored patch.')
for old,new,_ in replacements:
    section=section.replace(old,new,1)

# Default new profiles to the first image; legacy emoji values remain readable.
section=section.replace('string selectedAvatar = "🎮";', 'string selectedAvatar = "A01";')
section=section.replace('selectedAvatar = "🎮";', 'selectedAvatar = "A01";')
section=section.replace('"Selected avatar: 🎮"', '"Selected avatar: A01"')
section=section.replace('Avatar = "🎮"', 'Avatar = "A01"')

helper = '''    private static Control BuildAvatarVisual(string avatar, double size)
    {
        var file = System.IO.Path.Combine(
            AppContext.BaseDirectory, "Assets", "Avatars", avatar + ".png");

        if (System.IO.File.Exists(file))
        {
            try
            {
                return new Image
                {
                    Source = new Bitmap(file),
                    Width = size,
                    Height = size,
                    Stretch = Stretch.UniformToFill
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Could not load avatar " + file + ": " + ex.Message);
            }
        }

        return Text(avatar, 26, MainTextBrush);
    }

'''
updated = code[:start]+section+helper+code[end:]

if '</Project>' not in csproj:
    raise SystemExit('Unexpected .csproj format; no changes made.')
if 'Assets/Avatars/*.png' not in csproj:
    csproj = csproj.replace('</Project>', '''  <ItemGroup>
    <Content Include="Assets/Avatars/*.png" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>''',1)

src = root/'Assets/Avatars'
if len(list(src.glob('A*.png'))) != 16:
    raise SystemExit('Expected 16 avatar images; no changes made.')
dst = project/'src/XSphere/Assets/Avatars'
dst.mkdir(parents=True,exist_ok=True)
shutil.copy2(main_path,main_path.with_name('MainWindow.cs.before-image-avatars'))
shutil.copy2(csproj_path,csproj_path.with_name('XSphere.csproj.before-image-avatars'))
for image in src.glob('*.png'):
    shutil.copy2(image,dst/image.name)
main_path.write_text(updated)
csproj_path.write_text(csproj)
print('Installed 16 PNG avatars, updated selector and .csproj.')
print('Next: dotnet build && dotnet run --project src/XSphere')
