﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PseudoPopParser {

	class PopParser {

		private static IniFile _INI = new IniFile(@"config.ini");
		private static int number_of_warnings = 0;
		private static bool error_occurred = false;
		private static string datatypes_folder_path = "";
		private static List<List<int>> wave_credits_list = new List<List<int>>();
		private static int total_waves = 0;
		private static Dictionary<string, string> attribute_pairs = new Dictionary<string, string>();
		private static List<string> tfbot_templates = new List<string>();
		private static List<string> wavespawn_templates = new List<string>();
		private static List<List<string>> wave_wavespawn_names = new List<List<string>>();
		private static List<string> used_wavespawn_names = new List<string>();
		private static List<int> used_wavespawn_lines = new List<int>();

		// TODO Fix spaces vs tabs issue
		/* Purpose of Class:
		 * Check and return token types
		 * Throw exceptions if invalid token recieved
		 */

		private static string[] DATATYPES = {
			"$any_valid_string",		// Special
			"SUPPORT TYPE",				// Collection, special case 1
			"BOT NAME",					// Cannot use special characters
			"STRING",					// Primitive
			"INTEGER",					// Primitive
			"FLOAT",					// Primitive
			"SKILL",					// Collection
			"WEAPON RESTRICTION",		// Collection
			"BEHAVIOR MODIFIER",		// Collection
			"UNSIGNED INTEGER",			// Primitive
			"WHERE",
			"TEMPLATE NAME",
			"$item_attribute",
			"$item_attribute_value",
			"$char_attribute",
			"$char_attribute_value",
			"ITEM NAME",
			"ATTRIBUTE NAME",			// Collection
			"CLASS NAME",				// Collection
			"SENTRYGUN LEVEL",			// Collection
			"FILE",
			"EVENT POP FILE",			// Collection
			"BOOLEAN",					// Primitive Collection
			"MISSION OBJECTIVE",		// Collection
			"NAV FILTER AREA",			// Collection
		};

		// Base Constructor
		public PopParser(string folder) {
			datatypes_folder_path = folder;
		}

		// Config exists
		private int ConfigRead(string key) {
			if (_INI.KeyExists(key, "Global")) {
				return Int32.Parse(_INI.Read(key, "Global"));
			}
			return 1;
		}

		// Exists in 2D String List
		private bool ExistsTwoDimension (List<List<string>> list, string value) {
			foreach (List<string> sublist in list) {
				foreach (string member in sublist) {
					if (member == value) {
						return true;
					}
				}
			}
			return false;
		}

		// Print Formatted Contents of Credits List
		public void WriteCreditStats() {
			InfoLine("Credit Stats:");
			for (int i = 1; i <= wave_credits_list.Count; i++) {
				List<int> wave_credits = wave_credits_list[i - 1];
				InfoLine("\tW" + i + ": " + wave_credits.Sum() + " dropped during this wave");
				foreach (int credits in wave_credits) {
					InfoLine("\t\t" + credits);
				}
			}

			Console.Write("\n");
		}

		// Print Formatted Contents of WaveSpawn Names List
		public void WriteWaveSpawnNames() {
			InfoLine("Subwave Names:");
			for (int i = 1; i <= wave_wavespawn_names.Count; i++) {
				List<string> wave_names = wave_wavespawn_names[i - 1];
				InfoLine("\tW" + i + ": ");
				foreach (string name in wave_names) {
					InfoLine("\t\t" + name);
				}
			}
		}

		// Parse Collections
		public void ParseCollection(string token, int line = -1) {
			token = token.ToUpper();
			switch (token) {
				case "WAVE{}":

					// Increment wave counter
					total_waves++;

					// New credits list
					wave_credits_list.Add(new List<int>());

					// Separate wavespawn names
					wave_wavespawn_names.Add(new List<string>());

					break;

				// TODO : Add more cases
			}
		}

		// Trigger Collection End
		public void ParseCollectionEnd(string token, int line = -1) {
			token = token.ToUpper();
			switch (token) {
				case "WAVE{}":

					// Warn wave credits nonmultiple
					int wave_credits = wave_credits_list.Last().Sum();
					int credits_multiple = ConfigRead("currency_multiple_warning");
					if (!IsMultiple(wave_credits, credits_multiple)) {
						Warn("Wave " + total_waves + "'s credits is not a multiple of " + credits_multiple + ": ", -1, wave_credits.ToString());
					}

					break;

				case "ANY_VALID_STRING{}%": // End of Entire Schedule Second Pass Parsing
					// Second pass parsing does not require look ahead or look behind due to everything's already looked at.

					// Warn physical money counter credits >30000
					int total_credits = StartingCurrency + TotalCurrency + TotalWaveBonus;
					if (total_credits > 30000) {
						Warn("Credits counter physically cannot exceed reading of 30000: ", -1, total_credits.ToString());
					}

					// Compare WaveSpawn Names
					//foreach(string waitforname in used_wavespawn_names) {
					for(int i = 0; i < used_wavespawn_names.Count(); i++) {
						string waitforname = used_wavespawn_names[i];
						if (!ExistsTwoDimension(wave_wavespawn_names, waitforname)) {
							Warn("WaitForAllSpawned name does not exist: ", used_wavespawn_lines[i], waitforname);
						}
					}

					break;

				// TODO : Add more cases
			}
		}

		// Parse Key Value
		public void ParseKeyValue (string key, string value, int line = -1, string parent = "") {
			key = key.ToUpper();
			parent = RemoveCurly(parent.ToUpper());
			switch (key) {
				case "TOTALCURRENCY":
					int credits = Int32.Parse(value);

					// Warn negative or zero value
					if (credits > 0) {
						wave_credits_list.Last().Add(credits);
					}
					else if (credits == 0) {
						Warn("TotalCurrency value equal to 0 drops nothing: ", line, value);
					}
					else if (credits < 0) {
						Warn("TotalCurrency value less than 0 drops nothing: ", line, value);
					}
					break;

				case "STARTINGCURRENCY":
					attribute_pairs["STARTINGCURRENCY"] = Math.Max((int)0, Int32.Parse(value)).ToString();
					break;

				case "HEALTH":
					int health = Int32.Parse(value);
					int tank_warn_max = ConfigRead("tank_warn_maximum");
					int tank_warn_min = ConfigRead("tank_warn_minimum");
					int bot_health_multiple = ConfigRead("bot_health_multiple");
					int tank_health_multiple = ConfigRead("tank_health_multiple");

					if (parent == "TFBOT") {

						// Warn multiple
						if (!IsMultiple(health, bot_health_multiple)) {
							Warn("TFBot Health is not a multiple of " + bot_health_multiple.ToString() + ": ", line, value); //TODO Add line number and token
						}
					}
					else if (parent == "TANK") {

						if (!IsMultiple(health, tank_health_multiple)) {
							Warn("Tank Health is not a multiple of " + tank_health_multiple.ToString() + ": ", line, value); //TODO Add line number and token
						}

						// Warn exceeds boundaries
						if (health > tank_warn_max) {
							Warn("Tank Health exceeds maximum warning: " + tank_warn_max + " < ", line, value);
							// PotentialFix("Did you add too many zeros?");
						}
						else if (health < tank_warn_min) {
							Warn("Tank Health below minimum warning: " + tank_warn_min + " > ", line, value);
							// PotentialFix("Are you missing any zeros?");
						}

					}
					break;

				case "NAME":
					if (parent == "WAVESPAWN") {
						wave_wavespawn_names.Last().Add(value);
					}
					break;

				case "WAITFORALLSPAWNED":
					/*if (!ExistsTwoDimension(wave_wavespawn_names, value)) {
						Warn("WaitForAllSpawned name does not exist: ", line, value);
					}*/

					used_wavespawn_names.Add(value); // TODO Major Refactor
					used_wavespawn_lines.Add(line);
					break;
				case "WAITFORALLDEAD":
					/*if (!ExistsTwoDimension(wave_wavespawn_names, value)) {
						Warn("WaitForAllDead name does not exist: ", line, value);
					}*/
					used_wavespawn_names.Add(value); // TODO Major Refactor
					used_wavespawn_lines.Add(line);
					break;

				// TODO : Add more cases
			}
		}

		// Check if Integer is a multiple of Value
		public bool IsMultiple(int value, int multiple) {
			return value % multiple == 0;
		}

		// Get Starting Currency
		public int StartingCurrency {
			get {
				try {
					return Int32.Parse(attribute_pairs["STARTINGCURRENCY"]);
				}
				// Catch KeyNotFoundException, return $0 starting credits
				catch {
					return 0;
				}
			}
		}

		// Get Total Dropped Credits
		public int TotalCurrency {
			get {
				int total_currency = 0;
				foreach (List<int> credits_list in wave_credits_list) {
					total_currency += credits_list.Sum();
				}
				return total_currency;
			}
		}

		// Get Total Possible Bonus Credits
		// Note: Total Possible Bonus != #waves * 100
		// Note: A bonus is not rewarded if the wave drops 0 credits.
		// Note: A bonus is not rewarded on the final wave.
		public int TotalWaveBonus {
			get {
				int total_bonus = 0;
				for (int i = 0; i < wave_credits_list.Count; i++) {
					List<int> credits_list = wave_credits_list[i];
					
					// Final wave does not give bonus
					if (i == wave_credits_list.Count - 1) {
						break;
					}

					// Only give bonus if the wave drops any credits
					if (credits_list.Count > 0) {
						total_bonus += 100;
					}
				}
				return total_bonus;
			}
		}

		// Get Number of Waves
		public int TotalWaves {
			get {
				return total_waves;
			}
		}

		// Returns name of collection token
		public string RemoveCurly (string collection_token) {
			return Regex.Replace(collection_token, "({})", ""); // Must be exactly "{}" sequence
		}

		// Returns primitive datatype of token
		// Pending Removal
		public string GetDataTypePrimitive(string token) {
			if (Regex.IsMatch(token, @"^\d+.\d+$")) return "FLOAT";
			else if (Regex.IsMatch(token, @"^\d+$")) return "UNSIGNED INTEGER";
			else if (Regex.IsMatch(token, @"^(-?)\d+$")) return "INTEGER";
			else if (Regex.IsMatch(token, @"^[false|true|yes|no|1|0]$")) return "BOOLEAN";
			else return "STRING";
		}


		// Simple Print Color
		public void WriteMain(string message, string header, int line = -1, ConsoleColor background = ConsoleColor.Black, ConsoleColor foreground = ConsoleColor.Gray) {
			Console.BackgroundColor = background;
			Console.ForegroundColor = foreground;

			// Write Header
			Console.Write(header);
			Console.ResetColor();

			// Line Number and Message
			if (line > 0) { // Line Number exists
				Console.Write(":" + line + "\t" + message);
			}
			else { // Line number does not exist
				Console.Write("\t" + message);
			}
		}

		public void WriteMainLine(string message, string header, int line = -1, ConsoleColor background = ConsoleColor.Black, ConsoleColor foreground = ConsoleColor.Gray) {
			WriteMain(message, header, line, background, foreground);
			Console.Write("\n");
		}

		public void WriteColor(string message, ConsoleColor background = ConsoleColor.Black, ConsoleColor foreground = ConsoleColor.White) {
			Console.BackgroundColor = background;
			Console.ForegroundColor = foreground;
			Console.Write(message);
			Console.ResetColor();
		}

		// Simple Print Color Line
		public void WriteLineColor(string message, ConsoleColor background = ConsoleColor.Black, ConsoleColor foreground = ConsoleColor.Gray) {
			WriteColor(message, background, foreground);
			Console.Write("\n");
		}

		// Simple Print Potential Fix
		public void PotentialFix(string message, bool false_positive = false) {
			Console.Write("\t");
			Console.BackgroundColor = ConsoleColor.Gray;
			Console.ForegroundColor = ConsoleColor.Black;
			if (false_positive) {
				WriteMainLine(message, "[Ptl. False Positive]", -1, ConsoleColor.Gray, ConsoleColor.Black);
			}
			else {
				WriteMainLine(message, "[Potential Fix]", -1, ConsoleColor.Gray, ConsoleColor.Black);
			}
		}

		// Simple Print Warning
		public void Warn(string message, int line = -1, string token = "") {
			number_of_warnings++;
			ConsoleColor background = ConsoleColor.Yellow;
			ConsoleColor foreground = ConsoleColor.Black;

			WriteMain(message, "[Warning]", line, background, foreground);

			if (token.Length > 0) {
				Console.Write("'");
				WriteColor(token, ConsoleColor.Black, background);
				Console.Write("'\n");
			}
			else {
				Console.Write("\n");
			}
		}

		// Simple Print Error
		public void Error(string message, int line = -1, string token = "") {
			error_occurred = true;
			ConsoleColor background = ConsoleColor.Red;
			ConsoleColor foreground = ConsoleColor.Black;

			WriteMain(message, "[ERROR]", line, background, foreground);

			if (token.Length > 0) {
				Console.Write("'");
				WriteColor(token, ConsoleColor.Black, background);
				Console.Write("'\n");
			}
			else {
				Console.Write("\n");
			}
		}
		
		// Simple Print Error
		public void InfoLine(string message) {
			ConsoleColor background = ConsoleColor.DarkCyan;
			ConsoleColor foreground = ConsoleColor.Black;

			WriteMainLine(message, "[Info]", -1, background, foreground);
		}

		// Get number of warnings issued
		public int Warnings {
			get {
				return number_of_warnings;
			}
		}

		// Get if an error ever happened
		public bool ErrorOccurred {
			get {
				return error_occurred;
			}
		}

		// Returns if token falls under specified datatype
		/* Throws
		 * DatatypeNotFoundException
		 * InvalidTypeException
		 */
		public bool IsDatatype(string type, string token, int line_number = -1) {
			// Check valid datatype according to DATATYPES array
			foreach (string datatype in DATATYPES) {
				if (type.ToLower() == datatype.ToLower()) {

					// Check primitives first
					// TODO Change this to a switch statement
					if (type.ToUpper() == "BOOLEAN") {
						return Regex.IsMatch(token, @"^(false|true|yes|no|1|0)$", RegexOptions.IgnoreCase);
					}
					else if (type.ToUpper() == "FLOAT") {
						if (IsDatatype("INTEGER", token, line_number)) { // Float can be interpreted as Integer
							return IsDatatype("INTEGER", token, line_number);
						}
						return Regex.IsMatch(token, @"^\d+.\d+$");
					}
					else if (type.ToUpper() == "UNSIGNED INTEGER") {

						// Warn for Negative Values
						if (Regex.IsMatch(token, "-")) {
							Warn("Negative value will be interpreted as 0: ", line_number, token);
							return IsDatatype("INTEGER", token, line_number);
						}

						return Regex.IsMatch(token, @"^\d+$");
					}
					else if (type.ToUpper() == "INTEGER") {
						return Regex.IsMatch(token, @"^(-?)\d+$");
					}
					else if (type.ToUpper() == "STRING") {
						return !String.IsNullOrWhiteSpace(token); // return false on empty or blank string; should always 
					}
					else if (type.ToUpper() == "SENTRYGUN LEVEL") {
						return Regex.IsMatch(token, @"^[1|2|3]$");
					}
					else if (type.ToUpper() == @"$ANY_VALID_STRING") {
						return !Regex.IsMatch(token, "^(\\S|\\s|)*(\"|\\s|#base|#include)(\\S|\\s|)*$", RegexOptions.IgnoreCase);
					}
					else if (type.ToUpper() == "BOT NAME") { // Bot Name
						if (Regex.IsMatch(token, "%"))
							Warn("Invalid symbol \"%\"", line_number);
						if (Regex.IsMatch(token, "#"))
							Warn("Invalid symbol \"#\"", line_number);
						return true;
					}
					else if (type.ToUpper() == "ITEM NAME") {
						return IsDatatype("STRING", token, line_number); // fall back to any string for now
					}

					else if (type.ToUpper() == "WHERE") {
						return IsDatatype("STRING", token, line_number); // fall back to any string for now
					}
					else if (type.ToUpper() == "TEMPLATE NAME") {
						return IsDatatype("STRING", token, line_number); // fall back to any string for now
					}

					else if (type.ToUpper() == "SUPPORT TYPE") {

						// Warn 'limited' support is equal to having no support key
						if (token.ToUpper() == "LIMITED") {
							Warn("Support value disables infinite spawn: ", line_number, token);
							PotentialFix("Support 'limited' is the same as no support.");
						}
						return IsDatatype("STRING", token, line_number);
					}
					else if (type.ToUpper() == "FILE") {
						return Regex.IsMatch(token, @"\S+\.pop$", RegexOptions.IgnoreCase);
					}
					else if (type.ToUpper() == "CLASS NAME") {
						type = Regex.Replace(type, " ", "_");
						string[] datatype_file = File.ReadAllLines(datatypes_folder_path + "\\datatypes\\" + type + ".txt");
						foreach (string line in datatype_file) {
							if (Regex.IsMatch(token, line, RegexOptions.IgnoreCase)) {
								return true;
							}
						}
						return false;
					}

					// Check special datatypes according to datatypes definitions folder
					else {
						try {
							type = Regex.Replace(type, " ", "_");
							string[] datatype_file = File.ReadAllLines(datatypes_folder_path + "\\datatypes\\" + type + ".txt");
							foreach (string line in datatype_file) {
								if (line.ToUpper() == token.ToUpper()) {
									return true;
								}
							}
							return false;
						}
						catch (FileNotFoundException) {
							throw new Exception("DatatypeNotFoundException");
						}
					}

				}
			}
			throw new Exception("InvalidTypeException");
		}
	}

}