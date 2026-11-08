using System;
using System.Threading;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Swyf.CustomAI;

internal static class Program
{
    private static void Main()
    {
        var request=JObject.Parse("{\"resolve_prompt_version\":true,\"ai_category\":\"dialogue\",\"gameplay_route\":\"official\"}");
        var before=request.ToString();
        if(Runtime.TryComplete(request,CancellationToken.None,false,out var result))
            throw new Exception("Prompt metadata was intercepted as text generation.");
        if(request.ToString()!=before)throw new Exception("Game control request changed.");
        Console.WriteLine("PASS: compiled bridge leaves prompt lookup untouched without starting Unity or the provider.");
        var directory=(string)typeof(Runtime).GetProperty("ModDirectory",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        Directory.CreateDirectory(directory);
        var config=Path.Combine(directory,"settings.json");
        var prior=File.Exists(config)?File.ReadAllBytes(config):null;
        try
        {
            var options=new FakeOptions{GameplayRoute="official"};
            File.WriteAllText(config,"{\"enabled\":false}");
            if(Runtime.UseCustomProviderForGameEntry())throw new Exception("Disabled mod changed game entry.");
            Runtime.PrepareConversation(options);
            if(options.GameplayRoute!="official")throw new Exception("Disabled mod changed routing.");
            File.WriteAllText(config,"{\"enabled\":true}");
            if(!Runtime.IsCustomProviderReady(true,true,true,"success"))throw new Exception("Validated custom provider did not satisfy the AI-backend gate.");
            if(Runtime.IsCustomProviderReady(true,true,true,"untested") || Runtime.IsCustomProviderReady(true,true,true,"error"))
                throw new Exception("Unvalidated or failed provider incorrectly satisfied the AI-backend gate.");
            if(Runtime.IsCustomProviderReady(false,true,true,"success") || Runtime.IsCustomProviderReady(true,false,true,"success") ||
                Runtime.IsCustomProviderReady(true,true,false,"success"))
                throw new Exception("Disabled or unavailable custom provider incorrectly satisfied the AI-backend gate.");
            Runtime.PrepareConversation(options);
            if(options.GameplayRoute!="openrouter")throw new Exception("Custom provider did not use local prompts.");
            Console.WriteLine("PASS: only an enabled, available, successfully validated custom provider satisfies the backend gate.");
            Console.WriteLine("PASS: custom provider selects local prompt preparation; disabled mod preserves original route.");
            File.Delete(config);
            if(Runtime.IsCustomProviderReady(false,true,true,"success"))throw new Exception("Disabled mod changed game entry.");
            Console.WriteLine("PASS: disabled or missing settings preserve the original game AI sign-in requirement.");
        }
        finally{if(prior==null)File.Delete(config);else File.WriteAllBytes(config,prior);}
    }
}
public sealed class FakeOptions { public string GameplayRoute { get; set; } = ""; }
