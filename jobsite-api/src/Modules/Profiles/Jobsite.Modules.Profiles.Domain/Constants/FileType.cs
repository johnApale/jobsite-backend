namespace Jobsite.Modules.Profiles.Domain.Constants;

/// <summary>
/// Allowed file types for resume uploads.
/// Values must match the CHECK constraint <c>chk_resumes_file_type</c> exactly.
/// </summary>
public static class FileType
{
    public const string Pdf = "PDF";
    public const string Docx = "DOCX";

    public static bool IsValid(string fileType) =>
        fileType is Pdf or Docx;
}
