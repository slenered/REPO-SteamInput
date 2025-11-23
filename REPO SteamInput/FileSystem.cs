using System.IO;

namespace REPO_SteamInput;

public class FileSystem {
	internal static void CopyDirectory(string source, string destination, bool overwrite = false) {
		if (!Directory.Exists(destination))
			Directory.CreateDirectory(destination);
		else if (overwrite) {
			var backupFiles = Directory.GetFiles(destination);
			if (!Directory.Exists(destination + @"\backup"))
				Directory.CreateDirectory(destination + @"\backup");
			foreach (var backupFile in backupFiles) {
				File.Copy(backupFile, Path.Combine(destination + @"\backup", Path.GetFileName(backupFile)), true);
			}
		}
		
		var files = Directory.GetFiles(source);
		foreach (var file in files) {
			// REPO_SteamInput.Logger.LogInfo($"Copying {file} to {Path.Combine(destination, Path.GetFileName(file))}");
			File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite);
		}
	}
}