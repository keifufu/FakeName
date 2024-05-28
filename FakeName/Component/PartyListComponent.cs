using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FakeName.Config;
using FakeName.Utils;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FakeName.Component;

public class PartyListComponent : IDisposable
{
    private readonly PluginConfig config;
    
    private DateTime lastUpdate = DateTime.Today;
    public PartyListComponent(PluginConfig config)
    {
        this.config = config;
        
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnPartyListUpdate);
        
        
    }

    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(OnPartyListUpdate);
    }
    
    private void OnUpdate(IFramework framework)
    {
        try
        {
            if (DateTime.Now - lastUpdate > TimeSpan.FromSeconds(5))
            {
                RefreshPartyList();
                lastUpdate = DateTime.Now;
            }
        }
        catch (Exception e)
        {
            Service.Log.Error("PartyListComponent Err", e);
        }
    }
    
    private void OnPartyListUpdate(AddonEvent type, AddonArgs args)
    {
        try
        {
            if (DateTime.Now - lastUpdate > TimeSpan.FromSeconds(1))
            {
                RefreshPartyList();
                lastUpdate = DateTime.Now;
            }
        }
        catch (Exception e)
        {
            Service.Log.Error("PartyListComponent Err", e);
        }

        // Service.Log.Debug($"{type.ToString()} {args.AddonName} {Service.PartyList.Length}");
    }

    /**
     * 刷新小队列表
     */
    public unsafe void RefreshPartyList()
    {
        if (!config.Enabled)
        {
            return;
        }
        
        var localPlayer = Service.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            return;
        }
        
        var partyListMemberStructs = GetPartyListAddon();
        var cwProxy = InfoProxyCrossRealm.Instance();
        foreach (var memberStruct in partyListMemberStructs)
        {
            var nodeText = memberStruct.Name->NodeText.ToString();
            var nameNode = memberStruct.Name;
            // Service.Log.Debug($"partyList文本 {nodeText}");
            var match = Regex.Match(nodeText, "^(?:.*级\\s)?(?:\u0002\u0012\u0002Y\u0003)?\\s?(.*?)(?:\u0002\u001a\u0002\u0001\u0003)?$");
            if (match.Success)
            {
                var memberName = match.Groups[1].Value;
                // Service.Log.Debug($"匹配到文本 [{memberName}]");
                if (memberName.Equals(localPlayer.Name.TextValue))
                {
                    ReplaceSelf(memberName, localPlayer.HomeWorld.Id, nameNode);
                }
                else
                {
                    if (Service.PartyList.Any())
                    {
                        // 同服小队
                        ReplacePartyListHud(memberName, nameNode);
                    }
                    else
                    {
                        // 跨服小队
                        ReplaceCrossPartyListHud(memberName, nameNode, cwProxy);
                    }
                }
            }
        }
    }

    public unsafe void ReplaceSelf(string memberName, uint world, AtkTextNode* nameNode)
    {
        if (!config.TryGetCharacterConfig(memberName, world, out var characterConfig))
        {
            return;
        }
            
        nameNode->NodeText.SetString(nameNode->NodeText.ToString().Replace(memberName, characterConfig.FakeNameText));
    }

    public unsafe void ReplacePartyListHud(string memberName, AtkTextNode* nameNode)
    {
        foreach (var partyMember in Service.PartyList)
        {
            if (!partyMember.Name.TextValue.Equals(memberName))
            {
                continue;
            }

            if (!config.TryGetCharacterConfig(memberName, partyMember.World.Id, out var characterConfig))
            {
                continue;
            }
            
            nameNode->NodeText.SetString(nameNode->NodeText.ToString().Replace(memberName, characterConfig.FakeNameText));
            break;
        }
    }

    public unsafe void ReplaceCrossPartyListHud(string memberName, AtkTextNode* nameNode, InfoProxyCrossRealm* cwProxy)
    {
        var localIndex = cwProxy->LocalPlayerGroupIndex;
        var crossRealmGroup = cwProxy->CrossRealmGroupArraySpan[localIndex];
        
        for (var i = 0; i < crossRealmGroup.GroupMemberCount; i++)
        {
            var groupMember = crossRealmGroup.GroupMembersSpan[i];
            var groupMemberName = SeStringUtils.ReadSeString(groupMember.Name).TextValue;
            
            if (!groupMemberName.Equals(memberName))
            {
                continue;
            }

            if (!config.TryGetCharacterConfig(memberName, (ushort)groupMember.HomeWorld, out var characterConfig))
            {
                continue;
            }
            
            nameNode->NodeText.SetString(nameNode->NodeText.ToString().Replace(memberName, characterConfig.FakeNameText));
            // Service.Log.Debug($"{ nameNode->NodeText.ToString()}");
            break;
        }
    }

    private unsafe List<AddonPartyList.PartyListMemberStruct> GetPartyListAddon()
    {
        var partyListAddon = (AddonPartyList*) Service.GameGui.GetAddonByName("_PartyList", 1);
        
        List<AddonPartyList.PartyListMemberStruct> p = [
            partyListAddon->PartyMember.PartyMember0,
            partyListAddon->PartyMember.PartyMember1,
            partyListAddon->PartyMember.PartyMember2,
            partyListAddon->PartyMember.PartyMember3,
            partyListAddon->PartyMember.PartyMember4,
            partyListAddon->PartyMember.PartyMember5,
            partyListAddon->PartyMember.PartyMember6,
            partyListAddon->PartyMember.PartyMember7
        ];

        return p.Where(n => n.Name->NodeText.ToString().Length > 0).ToList();
    }
}
