﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using LuaInterface;
using ys;

namespace ComicSpider
{
	public class Lua_controller : Lua
	{
		public Lua_controller(bool wait_script_loading = true)
		{
			while (wait_script_loading && !Comic_spider.Is_script_loaded)
			{
				Thread.Sleep(300);
			}

			this["lc"] = this;

			main = MainWindow.Main;
			if (Dashboard.Is_initialized) dashboard = Dashboard.Instance;
			settings = Main_settings.Instance;

			try
			{
				this.DoString(Comic_spider.Lua_script);
			}
			catch (LuaException ex)
			{
				System.Windows.MessageBox.Show("comic_spider.lua : " + ex.Message + "\r\n" + ex.StackTrace);
			}
		}

		public MainWindow main;
		public Dashboard dashboard;
		public Main_settings settings;

		public void echo(string info)
		{
			try
			{
				Console.WriteLine(info);
				Dashboard.Instance.Dispatcher.Invoke(
					new Dashboard.Log_delegate(Dashboard.Instance.Log),
					info
				);
			}
			catch
			{
			}
		}

		public string format_for_number_sort(string str, int length = 3)
		{
			return ys.Common.Format_for_number_sort(str, length);
		}

		public string find(string pattern)
		{
			Match m = Regex.Match(this.GetString("html"), pattern, RegexOptions.IgnoreCase);
			if (!string.IsNullOrEmpty(m.Groups["find"].Value))
				return m.Groups["find"].Value;
			else if (!string.IsNullOrEmpty(m.Groups[1].Value))
				return m.Groups[1].Value;
			else
				return m.Groups[0].Value;
		}

		public void fill_list(string pattern, LuaFunction step = null)
		{
			Web_resource_info src_info = this["src_info"] as Web_resource_info;
			MatchCollection mc = Regex.Matches(this.GetString("html"), pattern, RegexOptions.IgnoreCase);
			for (var i = 0; i < mc.Count; i++)
			{
				this["url"] = mc[i].Groups["url"].Value;
				this["name"] = mc[i].Groups["name"].Value.Trim();

				if (step != null)
					(step as LuaFunction).Call(i, mc[i].Groups);

				add(
					this.GetString("url"),
					i,
					this.GetString("name"),
					src_info
				);
			}
		}
		public void fill_list(LuaTable patterns, LuaFunction step = null)
		{
			Web_resource_info src_info = this["src_info"] as Web_resource_info;
			MatchCollection mc;
			string all_sections = this.GetString("html");

			for (int i = 1; i < patterns.Values.Count; i++)
			{
				string sections = string.Empty;
				mc = Regex.Matches(all_sections, patterns[i] as string, RegexOptions.IgnoreCase);
				foreach (Match m in mc)
				{
					sections += '\n' + m.Groups[0].Value;
				}
				all_sections = sections;
			}

			mc = Regex.Matches(all_sections, patterns[patterns.Values.Count] as string, RegexOptions.IgnoreCase);
			for (var i = 0; i < mc.Count; i++)
			{
				this["url"] = mc[i].Groups["url"].Value;
				this["name"] = mc[i].Groups["name"].Value.Trim();

				if (step != null)
					(step as LuaFunction).Call(i, mc[i].Groups);

				add(
					this.GetString("url"),
					i,
					this.GetString("name"),
					src_info
				);
			}
		}
		public void fill_list(Newtonsoft.Json.Linq.JArray arr, LuaFunction step = null)
		{
			Web_resource_info src_info = this["src_info"] as Web_resource_info;
			for (int i = 0; i < arr.Count; i++)
			{
				this["url"] = arr[i].ToString();
				this["name"] = Path.GetFileName(this.GetString("url")).Trim();

				if (step != null)
					(step as LuaFunction).Call(i, arr[i].ToString());

				add(
					this.GetString("url"),
					i,
					this.GetString("name"),
					src_info
				);
			}
		}
		public void xfill_list(string selector, LuaFunction step)
		{
			Web_resource_info src_info = this["src_info"] as Web_resource_info;

			HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
			doc.LoadHtml(this.GetString("html"));

			HtmlAgilityPack.HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(selector);

			if (nodes == null) return;

			this["name"] = string.Empty;

			for (int i = 0; i < nodes.Count; i++)
			{
				if (step != null)
					(step as LuaFunction).Call(i, nodes[i]);

				if (string.IsNullOrEmpty(this.GetString("url")))
					continue;
				else
				{
					if (!string.IsNullOrEmpty(src_info.Name))
						this["name"] = this.GetString("name").Replace(src_info.Name, "").Trim();
				}

				add(
					this.GetString("url"),
					i,
					this.GetString("name"),
					src_info
				);
			}
		}

		public void add(string url, int index, string name, Web_resource_info parent = null)
		{
			if (string.IsNullOrEmpty(url))
				return;

			List<Web_resource_info> list = this["info_list"] as List<Web_resource_info>;

			list.Add(new Web_resource_info(
				url,
				index,
				name,
				"",
				parent)
			);
		}

		public int levenshtein_distance(string s, string t)
		{
			return ys.Common.LevenshteinDistance(s, t);
		}

		public object eval_js(string s)
		{
			using (Noesis.Javascript.JavascriptContext context = new Noesis.Javascript.JavascriptContext())
			{
				return context.Run(s);
			}
		}

		public object json_decode(string input)
		{
			return Newtonsoft.Json.JsonConvert.DeserializeObject(input);
		}

		public Web_client web_post(string url, LuaTable dict)
		{
			Dictionary<string, string> info = new Dictionary<string, string>();
			foreach (string key in dict.Keys)
			{
				info.Add(key, dict[key] as string);
			}
			return Web_client.Post(url, info);
		}

		public void login(string host, LuaTable dict = null)
		{
			string data = string.Empty;
			if (dict != null)
			{
				foreach (var key in dict.Keys)
				{
					data += System.Web.HttpUtility.UrlEncode(key as string) + "="
						+ System.Web.HttpUtility.UrlEncode(dict[key] as string);
				}
			}

			Web_client wc = new Web_client();
			string cookie = wc.DownloadString(
				"http://ysmood.org/comicspider/login/" +
				Uri.EscapeUriString(host) + "?" +
				data
			);
			Cookie_pool.Instance.Update(host, cookie);
		}
	}

	[Serializable]
	public class Lua_script
	{
		public Lua_script(string script, string hash, DateTime last_modified)
		{
			Script = script;
			E_tag = hash;
			Last_modified = last_modified;
		}

		public string Script { get; set; }
		public string E_tag { get; set; }
		public DateTime Last_modified { get; set; }
	}
}
