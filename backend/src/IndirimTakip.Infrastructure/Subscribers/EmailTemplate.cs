using System.Text.Encodings.Web;

namespace IndirimTakip.Infrastructure.Subscribers;

internal static class EmailTemplate
{
    internal const string ProductionFrontendUrl = "https://www.proteinavcisi.com.tr";

    internal static string AssetUrl(string frontendBaseUrl, string fileName) =>
        $"{frontendBaseUrl.TrimEnd('/')}/email-assets/{fileName}";

    internal static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

    internal static string Document(string preheader, string content) => $$"""
        <!doctype html>
        <html lang="tr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta name="color-scheme" content="light only">
          <meta name="supported-color-schemes" content="light only">
          <title>Protein Avcısı</title>
          <style>
            body, table, td, a { -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%; }
            table, td { mso-table-lspace:0pt; mso-table-rspace:0pt; }
            img { -ms-interpolation-mode:bicubic; border:0; outline:none; text-decoration:none; }
            table { border-collapse:collapse !important; }
            body { width:100% !important; min-width:100%; height:100% !important; margin:0 !important; padding:0 !important; background:#f6f7fb; }
            a[x-apple-data-detectors] { color:inherit !important; text-decoration:none !important; }
            @media only screen and (max-width:620px) {
              .email-shell { width:100% !important; border-radius:0 !important; }
              .email-pad { padding-left:20px !important; padding-right:20px !important; }
              .email-hero { padding:34px 22px !important; background-image:none !important; }
              .mobile-block { display:block !important; width:100% !important; max-width:100% !important; }
              .mobile-hide { display:none !important; max-height:0 !important; overflow:hidden !important; }
              .mobile-center { text-align:center !important; }
              .mobile-full { width:100% !important; }
              .product-cell { display:block !important; width:100% !important; border-right:0 !important; }
              .product-card { min-height:0 !important; }
              .product-image { width:88px !important; height:88px !important; }
              .step-cell { display:block !important; width:100% !important; padding:14px 0 !important; }
              .email-title { font-size:32px !important; line-height:1.08 !important; }
            }
          </style>
        </head>
        <body>
          <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;line-height:1px;font-size:1px;">{{Encode(preheader)}}&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;</div>
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#f6f7fb">
            <tr>
              <td align="center" style="padding:28px 12px;">
                {{content}}
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    internal static string BrandHeader(string frontendBaseUrl) => $"""
        <tr>
          <td align="center" bgcolor="#ffffff" style="padding:26px 24px;border-bottom:1px solid #eef0f6;">
            <a href="{Encode(frontendBaseUrl)}" style="display:inline-block;text-decoration:none;color:#171a2e;">
              <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                <tr>
                  <td align="center" valign="middle" width="44" height="44" bgcolor="#171b3f" style="width:44px;height:44px;border:1px solid #6556e8;border-radius:10px;color:#b5abfc;font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:800;line-height:44px;text-align:center;">PA</td>
                  <td valign="middle" style="padding-left:12px;font-family:Arial,Helvetica,sans-serif;font-size:20px;font-weight:800;line-height:24px;letter-spacing:-0.6px;color:#171a2e;white-space:nowrap;">PROTEİN<span style="color:#6556e8;">AVCISI</span></td>
                </tr>
              </table>
            </a>
          </td>
        </tr>
        """;

    internal static string BrandHeaderDark(string frontendBaseUrl) => $"""
        <tr>
          <td bgcolor="#0e1122" style="padding:26px 38px 8px;">
            <a href="{Encode(frontendBaseUrl)}" style="display:inline-block;text-decoration:none;color:#ffffff;">
              <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                <tr>
                  <td align="center" valign="middle" width="44" height="44" bgcolor="#171b3f" style="width:44px;height:44px;border:1px solid #6556e8;border-radius:10px;color:#b5abfc;font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:800;line-height:44px;text-align:center;">PA</td>
                  <td valign="middle" style="padding-left:12px;font-family:Arial,Helvetica,sans-serif;font-size:20px;font-weight:800;line-height:24px;letter-spacing:-0.6px;color:#ffffff;white-space:nowrap;">PROTEİN<span style="color:#9b8cff;">AVCISI</span></td>
                </tr>
              </table>
            </a>
          </td>
        </tr>
        """;

    internal static string PrimaryButton(string url, string label) => $"""
        <a href="{Encode(url)}" style="display:inline-block;background:#6556e8;color:#ffffff;text-decoration:none;font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:700;line-height:20px;padding:14px 28px;border-radius:9px;">{Encode(label)}</a>
        """;

    internal static string FullWidthButton(string url, string label) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
          <tr>
            <td align="center" bgcolor="#6556e8" style="border-radius:9px;">
              <a href="{Encode(url)}" style="display:block;color:#ffffff;text-decoration:none;font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:700;line-height:20px;padding:14px 24px;border-radius:9px;">{Encode(label)}</a>
            </td>
          </tr>
        </table>
        """;
}
