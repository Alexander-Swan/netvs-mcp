using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace NetVsMcp.Vsix;

/// <summary>
/// Simple WinForms "About NetVsMcp" dialog. Shown automatically the first time a new version of
/// the extension loads (see <see cref="FirstRunAboutService"/>), and can be reused later from a
/// menu command if one is added.
/// </summary>
internal sealed class AboutDialog : Form
{
    public AboutDialog()
    {
        Text = "About NetVsMcp";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 300);
        Font = new Font("Segoe UI", 9F);

        var icon = TryLoadIcon();
        if (icon is not null)
        {
            Icon = icon;
        }

        var iconBox = new PictureBox
        {
            Location = new Point(20, 20),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = icon?.ToBitmap(),
        };

        var titleLabel = new Label
        {
            Text = AboutInfo.DisplayName,
            Font = new Font("Segoe UI Semibold", 12F),
            Location = new Point(80, 20),
            Size = new Size(360, 26),
        };

        var versionLabel = new Label
        {
            Text = $"Version {AboutInfo.Version}",
            ForeColor = Color.DimGray,
            Location = new Point(80, 46),
            Size = new Size(360, 20),
        };

        var descriptionLabel = new Label
        {
            Text = AboutInfo.Description,
            Location = new Point(20, 84),
            Size = new Size(420, 60),
        };

        var downloadHeaderLabel = new Label
        {
            Text = "The NetVsMcp Broker must be running locally for MCP clients to connect:",
            Location = new Point(20, 154),
            Size = new Size(420, 20),
        };

        var downloadLink = new LinkLabel
        {
            Text = AboutInfo.BrokerDownloadUrl,
            Location = new Point(20, 176),
            Size = new Size(420, 20),
        };
        downloadLink.LinkClicked += (_, _) => OpenUrl(AboutInfo.BrokerDownloadUrl);

        var projectLink = new LinkLabel
        {
            Text = "Project page and documentation",
            Location = new Point(20, 202),
            Size = new Size(420, 20),
        };
        projectLink.LinkClicked += (_, _) => OpenUrl(AboutInfo.ProjectUrl);

        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(364, 240),
            Size = new Size(76, 28),
        };

        Controls.Add(iconBox);
        Controls.Add(titleLabel);
        Controls.Add(versionLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(downloadHeaderLabel);
        Controls.Add(downloadLink);
        Controls.Add(projectLink);
        Controls.Add(closeButton);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

    private static Icon? TryLoadIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NetVsMcp.Vsix.Assets.broker.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return null;
        }
    }
}
