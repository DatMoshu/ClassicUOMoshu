﻿// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Assets;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class SkillGumpAdvanced : Gump
    {
        private const int WIDTH = 500;
        private const int HEIGHT = 360;

        private readonly Dictionary<Buttons, string> _buttonsToSkillsValues = new Dictionary<
            Buttons,
            string
        >
        {
            { Buttons.SortName, "Name" },
            { Buttons.SortReal, "Base" },
            { Buttons.SortBase, "Value" },
            { Buttons.SortCap, "Cap" }
        };

        private readonly DataBox _databox;
        private readonly List<SkillListEntry> _skillListEntries = new List<SkillListEntry>();

        private bool _sortAsc;
        private string _sortField;
        private readonly GumpPic _sortOrderIndicator;
        private double _totalReal,
            _totalValue;
        private bool _updateSkillsNeeded;

        public SkillGumpAdvanced(World world) : base(world, 0, 0)
        {
            _totalReal = 0;
            _totalValue = 0;
            CanMove = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            Width = WIDTH;
            Height = HEIGHT;

            Add(
                new AlphaBlendControl(0.95f)
                {
                    X = 1,
                    Y = 1,
                    Width = WIDTH - 2,
                    Height = HEIGHT - 2
                }
            );

            ScrollArea area = new ScrollArea(20, 60, WIDTH - 40, 250, true)
            {
                AcceptMouseInput = true
            };

            Add(area);

            _databox = new DataBox(0, 0, 1, 1);
            _databox.WantUpdateSize = true;

            area.Add(_databox);

            Add(
                new NiceButton(10, 10, 180, 25, ButtonAction.Activate, ResGumps.Name)
                {
                    ButtonParameter = (int)Buttons.SortName,
                    IsSelected = true,
                    X = 40,
                    Y = 25
                }
            );

            Add(
                new NiceButton(10, 10, 80, 25, ButtonAction.Activate, ResGumps.Real)
                {
                    ButtonParameter = (int)Buttons.SortReal,
                    X = 220,
                    Y = 25
                }
            );

            Add(
                new NiceButton(10, 10, 80, 25, ButtonAction.Activate, ResGumps.Base)
                {
                    ButtonParameter = (int)Buttons.SortBase,
                    X = 300,
                    Y = 25
                }
            );

            Add(
                new NiceButton(10, 10, 80, 25, ButtonAction.Activate, ResGumps.Cap)
                {
                    ButtonParameter = (int)Buttons.SortCap,
                    X = 380,
                    Y = 25
                }
            );

            Add(new Line(20, 60, 435, 1, 0xFFFFFFFF));

            Add(new Line(20, 310, 435, 1, 0xFFFFFFFF));

            Add(_sortOrderIndicator = new GumpPic(0, 0, 0x985, 0));
            OnButtonClick((int)Buttons.SortName);
        }

        public override GumpType GumpType => GumpType.SkillMenu;

        public override void OnButtonClick(int buttonID)
        {
            if (_buttonsToSkillsValues.TryGetValue((Buttons)buttonID, out string fieldValue))
            {
                if (_sortField == fieldValue)
                {
                    _sortAsc = !_sortAsc;
                }

                _sortField = fieldValue;
            }

            if (FindControls<NiceButton>().Any(s => s.ButtonParameter == buttonID))
            {
                NiceButton btn = FindControls<NiceButton>()
                    .First(s => s.ButtonParameter == buttonID);

                ushort g = (ushort)(_sortAsc ? 0x985 : 0x983);

                _sortOrderIndicator.Graphic = g;
                _sortOrderIndicator.X = btn.X + btn.Width - 15;
                _sortOrderIndicator.Y = btn.Y + 5;
            }

            _updateSkillsNeeded = true;
        }

        private void BuildGump()
        {
            _totalReal = 0;
            _totalValue = 0;
            _databox.Clear();

            foreach (SkillListEntry entry in _skillListEntries)
            {
                entry.Clear();
                entry.Dispose();
            }

            _skillListEntries.Clear();

            PropertyInfo pi = typeof(Skill).GetProperty(_sortField);
            List<Skill> sortSkills = new List<Skill>(
                World.Player.Skills.OrderBy(x => pi.GetValue(x, null))
            );

            if (_sortAsc)
            {
                sortSkills.Reverse();
            }

            foreach (Skill skill in sortSkills)
            {
                _totalReal += skill.Base;
                _totalValue += skill.Value;

                Label skillName = new Label(skill.Name, true, 1153, font: 3);
                Label skillValueBase = new Label(skill.Base.ToString(), true, 1153, font: 3);
                Label skillValue = new Label(skill.Value.ToString(), true, 1153, font: 3);
                Label skillCap = new Label(skill.Cap.ToString(), true, 1153, font: 3);

                _skillListEntries.Add(
                    new SkillListEntry(this, skillName, skillValueBase, skillValue, skillCap, skill)
                );
            }

            foreach (SkillListEntry t in _skillListEntries)
            {
                _databox.Add(t);
            }

            _databox.WantUpdateSize = true;
            _databox.ReArrangeChildren();

            Add(new Label(ResGumps.Total, true, 1153) { X = 40, Y = 320 });
            Add(new Label(_totalReal.ToString("F1"), true, 1153) { X = 220, Y = 320 });
            Add(new Label(_totalValue.ToString("F1"), true, 1153) { X = 300, Y = 320 });
        }

        public override void Update()
        {
            base.Update();

            if (_updateSkillsNeeded)
            {
                foreach (Label label in Children.OfType<Label>())
                {
                    label.Dispose();
                }

                BuildGump();

                _updateSkillsNeeded = false;
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);

            batcher.DrawRectangle(
                SolidColorTextureCache.GetTexture(Color.Gray),
                x,
                y,
                Width,
                Height,
                hueVector
            );

            return base.Draw(batcher, x, y);
        }

        public void ForceUpdate()
        {
            _updateSkillsNeeded = true;
        }

        private enum Buttons
        {
            SortName = 1,
            SortReal = 2,
            SortBase = 3,
            SortCap = 4
        }
    }

    internal class SkillListEntry : Control
    {
        private readonly SkillGumpAdvanced _gump;
        private readonly Button _activeUse;
        private readonly Skill _skill;

        public SkillListEntry(
            SkillGumpAdvanced gump,
            Label skillname,
            Label skillvaluebase,
            Label skillvalue,
            Label skillcap,
            Skill skill
        )
        {
            _gump = gump;
            Height = 20;
            Label skillName = skillname;
            Label skillValueBase = skillvaluebase;
            Label skillValue = skillvalue;
            Label skillCap = skillcap;

            _skill = skill;
            skillName.X = 20;

            if (skill.IsClickable)
            {
                Add(
                    _activeUse = new Button((int)Buttons.ActiveSkillUse, 0x837, 0x838)
                    {
                        X = 0,
                        Y = 4,
                        ButtonAction = ButtonAction.Activate
                    }
                );
            }

            Add(skillName);
            skillValueBase.X = 200;

            Add(skillValueBase);
            skillValue.X = 280;

            Add(skillValue);
            skillCap.X = 360;

            Add(skillCap);

            GumpPic loc = new GumpPic(
                425,
                4,
                (ushort)(
                    skill.Lock == Lock.Up
                        ? 0x983
                        : skill.Lock == Lock.Down
                            ? 0x985
                            : 0x82C
                ),
                0
            );

            Add(loc);

            loc.MouseUp += (sender, e) =>
            {
                switch (_skill.Lock)
                {
                    case Lock.Up:
                        _skill.Lock = Lock.Down;
                        GameActions.ChangeSkillLockStatus((ushort)_skill.Index, (byte)Lock.Down);
                        loc.Graphic = 0x985;

                        break;

                    case Lock.Down:
                        _skill.Lock = Lock.Locked;
                        GameActions.ChangeSkillLockStatus((ushort)_skill.Index, (byte)Lock.Locked);
                        loc.Graphic = 0x82C;

                        break;

                    case Lock.Locked:
                        _skill.Lock = Lock.Up;
                        GameActions.ChangeSkillLockStatus((ushort)_skill.Index, (byte)Lock.Up);
                        loc.Graphic = 0x983;

                        break;
                }
            };
        }

        protected override void OnDragBegin(int x, int y)
        {
            if (_skill.IsClickable && Mouse.LButtonPressed)
            {
                GetSpellFloatingButton(_skill.Index)?.Dispose();

                ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(0x24B8);

                SkillButtonGump skillButtonGump = new SkillButtonGump(
                    _gump.World,
                    _skill,
                    Mouse.LClickPosition.X + (gumpInfo.UV.Width >> 1),
                    Mouse.LClickPosition.Y + (gumpInfo.UV.Height >> 1)
                );

                UIManager.Add(skillButtonGump);
                UIManager.AttemptDragControl(skillButtonGump, true);
            }
        }

        private static SkillButtonGump GetSpellFloatingButton(int id)
        {
            for (LinkedListNode<Gump> i = UIManager.Gumps.Last; i != null; i = i.Previous)
            {
                if (i.Value is SkillButtonGump g && g.SkillID == id)
                {
                    return g;
                }
            }

            return null;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch ((Buttons)buttonID)
            {
                case Buttons.ActiveSkillUse:
                    GameActions.UseSkill(_skill.Index);

                    break;
            }
        }

        private enum Buttons
        {
            ActiveSkillUse = 1
        }
    }
}
