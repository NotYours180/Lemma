﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Windows.Forms;
using GeeUI.Managers;
using GeeUI.ViewLayouts;
using GeeUI.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using System.Collections;
using Microsoft.Xna.Framework.Input;
using System.Xml.Serialization;
using Lemma.Util;
using ComponentBind;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using ListView = GeeUI.Views.ListView;
using View = GeeUI.Views.View;

namespace Lemma.Components
{
	public class EditorGeeUI : Component<Main>
	{
		public struct PopupCommand
		{
			public string Description;
			public PCInput.Chord Chord;
			public Command Action;
			public Func<bool> Enabled;
		}

		private const float precisionDelta = 0.025f;
		private const float normalDelta = 1.0f;
		private const float stringNavigateInterval = 0.08f;

		[XmlIgnore]
		public View RootEditorView;

		[XmlIgnore]
		public TabHost ComponentTabViews;

		[XmlIgnore]
		public PanelView ActionsPanelView;

		[XmlIgnore]
		public DropDownView CreateDropDownView;

		[XmlIgnore]
		public PanelView LinkerView;

		[XmlIgnore]
		public ListProperty<Entity> SelectedEntities = new ListProperty<Entity>();

		[XmlIgnore]
		public Property<bool> MapEditMode = new Property<bool>();

		[XmlIgnore]
		public Property<bool> EnablePrecision = new Property<bool>();

		[XmlIgnore]
		public ListProperty<PopupCommand> PopupCommands = new ListProperty<PopupCommand>();

		[XmlIgnore]
		public Property<bool> NeedsSave = new Property<bool>();


		private SpriteFont MainFont;

		public override void Awake()
		{
			base.Awake();
			MainFont = main.Content.Load<SpriteFont>("EditorFont");

			this.RootEditorView = new View(main.GeeUI, main.GeeUI.RootView);
			this.ComponentTabViews = new TabHost(main.GeeUI, RootEditorView, Vector2.Zero, MainFont);
			this.ActionsPanelView = new PanelView(main.GeeUI, RootEditorView, Vector2.Zero);
			var dropDownPlusLabel = new View(main.GeeUI, ActionsPanelView);
			dropDownPlusLabel.ChildrenLayouts.Add(new VerticalViewLayout(2, false));
			dropDownPlusLabel.ChildrenLayouts.Add(new ExpandToFitLayout());
			dropDownPlusLabel.X = 20;
			dropDownPlusLabel.Y = 5;
			new TextView(main.GeeUI, dropDownPlusLabel, "Actions:", Vector2.Zero, MainFont);
			this.CreateDropDownView = new DropDownView(main.GeeUI, dropDownPlusLabel, Vector2.Zero, MainFont);
			var openLinkerButton = new ButtonView(main.GeeUI, dropDownPlusLabel, "Command Linker", Vector2.Zero, MainFont)
			{
				Name = "LinkerButton"
			};
			openLinkerButton.OnMouseClick += (sender, args) =>
			{
				ShowLinkerView();
			};

			openLinkerButton.Active.Value = false;
			ActionsPanelView.Draggable = false;

			RootEditorView.Add(new Binding<int, Point>(RootEditorView.Width, point => point.X, main.ScreenSize));
			ComponentTabViews.Add(new Binding<int, int>(ComponentTabViews.Width, i => i / 2, RootEditorView.Width));
			ActionsPanelView.Add(new Binding<int, int>(ActionsPanelView.Width, i => i / 2, RootEditorView.Width));
			ActionsPanelView.Add(new Binding<Vector2, int>(ActionsPanelView.Position, i => new Vector2(i / 2f, 25), RootEditorView.Width));

			LinkerView = new PanelView(main.GeeUI, main.GeeUI.RootView, Vector2.Zero);
			LinkerView.Width.Value = 420;
			LinkerView.Height.Value = 400;
			LinkerView.Draggable = false;
			LinkerView.Active.Value = false;

			var addButton = new ButtonView(main.GeeUI, LinkerView, "Add", Vector2.Zero, MainFont) { Name = "AddButton" };

			var SourceLayout = new View(main.GeeUI, LinkerView) { Name = "SourceLayout" };
			SourceLayout.ChildrenLayouts.Add(new VerticalViewLayout(2, false));
			SourceLayout.ChildrenLayouts.Add(new ExpandToFitLayout());
			new TextView(main.GeeUI, SourceLayout, "Source command:", Vector2.Zero, MainFont);
			new DropDownView(main.GeeUI, SourceLayout, new Vector2(), MainFont)
			{
				Name = "SourceDropDown"
			};

			var DestLayout = new View(main.GeeUI, LinkerView) { Name = "DestLayout" };
			DestLayout.ChildrenLayouts.Add(new VerticalViewLayout(2, false));
			DestLayout.ChildrenLayouts.Add(new ExpandToFitLayout());
			new TextView(main.GeeUI, DestLayout, "Destination entity:", Vector2.Zero, MainFont);
			new DropDownView(main.GeeUI, DestLayout, new Vector2(), MainFont)
			{
				Name = "DestEntityDropDown"
			};
			//Padding
			new View(main.GeeUI, DestLayout).Height.Value = 20;
			new TextView(main.GeeUI, DestLayout, "Destination command:", Vector2.Zero, MainFont);
			new DropDownView(main.GeeUI, DestLayout, new Vector2(), MainFont)
			{
				Name = "DestCommandDropDown"
			};

			var ListLayout = new View(main.GeeUI, LinkerView);
			ListLayout.ChildrenLayouts.Add(new VerticalViewLayout(2, false));
			new TextView(main.GeeUI, ListLayout, "Linked commands:", Vector2.Zero, MainFont);
			var container = new PanelView(main.GeeUI, ListLayout, Vector2.Zero);
			var list = new ListView(main.GeeUI, container) { Name = "CurLinksList" };
			list.Position.Value = new Vector2(5);


			list.ChildrenLayouts.Add(new VerticalViewLayout(1, false));
			ListLayout.Add(new Binding<int>(ListLayout.Width, LinkerView.Width));
			container.Add(new Binding<int>(container.Width, (i) => i - 8, ListLayout.Width));
			list.Add(new Binding<int>(list.Width, container.Width));

			ListLayout.Position.Value = new Vector2(5, 150);

			ListLayout.Height.Value = container.Height.Value = list.Height.Value = (LinkerView.Height.Value - ListLayout.Y) - 3;
			SourceLayout.Position.Value = new Vector2(20, 20);
			DestLayout.Position.Value = new Vector2(260, 20);

			addButton.AnchorPoint.Value = new Vector2(0, 1);
			addButton.Position.Value = new Vector2(20, 130);
			addButton.Active.Value = false;

			LinkerView.OnMouseClickAway += (sender, args) =>
			{
				if (!LinkerView.AbsoluteBoundBox.Contains(InputManager.GetMousePos()))
					HideLinkerView();
			};

			HideLinkerView();

			RootEditorView.Height.Value = 160;
			ComponentTabViews.Height.Value = 160;
			ActionsPanelView.Height.Value = 125;

			this.SelectedEntities.ItemAdded += new ListProperty<Entity>.ItemAddedEventHandler(delegate(int index, Entity item)
			{
				this.refresh();
			});
			this.SelectedEntities.ItemRemoved += new ListProperty<Entity>.ItemRemovedEventHandler(delegate(int index, Entity item)
			{
				this.refresh();
			});
			this.SelectedEntities.ItemChanged += new ListProperty<ComponentBind.Entity>.ItemChangedEventHandler(delegate(int index, Entity old, Entity newValue)
			{
				this.refresh();
			});
			this.SelectedEntities.Cleared += new ListProperty<ComponentBind.Entity>.ClearEventHandler(this.refresh);
			this.Add(new NotifyBinding(this.refresh, this.MapEditMode));

			this.PopupCommands.ItemAdded += (index, command) =>
			{
				RecomputePopupCommands();
			};
			this.PopupCommands.ItemChanged += (index, old, value) =>
			{
				RecomputePopupCommands();
			};
			this.PopupCommands.ItemRemoved += (index, command) =>
			{
				RecomputePopupCommands();
			};
		}

		public void HideLinkerView()
		{
			LinkerView.Active.Value = false;
		}

		public void ShowLinkerView()
		{
			if (SelectedEntities.Count != 1) return;
			LinkerView.Active.Value = true;
			LinkerView.Position.Value = InputManager.GetMousePosV();
			BindLinker(SelectedEntities[0]);
		}

		public void BindLinker(Entity e)
		{
			var sourceDrop = ((DropDownView)LinkerView.FindFirstChildByName("SourceDropDown"));
			var destEntityDrop = ((DropDownView)LinkerView.FindFirstChildByName("DestEntityDropDown"));
			var destCommDrop = ((DropDownView)LinkerView.FindFirstChildByName("DestCommandDropDown"));
			var addButton = ((ButtonView)LinkerView.FindFirstChildByName("AddButton"));
			var listView = ((ListView)LinkerView.FindFirstChildByName("CurLinksList"));

			addButton.ResetOnMouseClick();

			sourceDrop.RemoveAllOptions();
			destEntityDrop.RemoveAllOptions();
			destCommDrop.RemoveAllOptions();

			#region Actions
			Action fillDestEntity = () =>
			{
				foreach (var ent in main.Entities)
				{
					if (ent == e) continue;
					//IDK this seems like a good one to use!
					if (ent.EditorCanDelete)
					{
						destEntityDrop.AddOption(ent.ID + "[" + ent.GUID + "]", () => { }, null, ent);
					}
				}
			};

			Action<Entity> fillDestCommand = (ent) =>
			{
				if (ent == null) return;
				foreach (var comm in ent.Commands)
				{
					var c = comm.Value as Command;
					if (c == null) continue;
					destCommDrop.AddOption(comm.Key.ToString(), () => { }, null, c);
				}
			};

			Action<bool, bool, bool> recompute = (sourceChanged, destEntityChanged, destCommChanged) =>
			{
				if (sourceChanged)
				{
					destEntityDrop.RemoveAllOptions();
					destCommDrop.RemoveAllOptions();
					fillDestEntity();
				}

				if (destEntityChanged)
				{
					destCommDrop.RemoveAllOptions();
					var selected = destEntityDrop.GetSelectedOption();
					if (selected == null) return;
					fillDestCommand(selected.Related as Entity);
				}

				destEntityDrop.Active.Value = sourceDrop.LastItemSelected.Value != -1;
				destCommDrop.Active.Value = destEntityDrop.LastItemSelected.Value != -1 && destEntityDrop.Active;
				addButton.Active.Value = destCommDrop.Active && destCommDrop.LastItemSelected.Value != -1;
			};

			Action populateList = null;
			populateList = () =>
			{
				listView.RemoveAllChildren();
				listView.ContentOffset.Value = Vector2.Zero;
				List<Entity.CommandLink> toRemove = new List<Entity.CommandLink>();
				foreach (var link in e.LinkedCommands)
				{
					Entity target = link.TargetEntity.Target;
					if (target == null)
					{
						toRemove.Add(link);
						continue;
					}
					View container = new View(main.GeeUI, listView);
					container.ChildrenLayouts.Add(new HorizontalViewLayout(4));
					container.ChildrenLayouts.Add(new ExpandToFitLayout());
					var sourceView = new TextView(main.GeeUI, container, link.SourceCommand, Vector2.Zero, MainFont);
					var entView = new TextView(main.GeeUI, container, target.ID + " [" + target.GUID + "]", Vector2.Zero, MainFont);
					var destView = new TextView(main.GeeUI, container, link.TargetCommand, Vector2.Zero, MainFont);

					sourceView.AutoSize.Value = false;
					entView.AutoSize.Value = false;
					destView.AutoSize.Value = false;

					sourceView.Width.Value = entView.Width.Value = destView.Width.Value = 125;
					sourceView.TextJustification = TextJustification.Left;
					entView.TextJustification = TextJustification.Center;
					destView.TextJustification = TextJustification.Right;

					var button = new ButtonView(main.GeeUI, container, "-", Vector2.Zero, MainFont);
					button.OnMouseClick += (sender, args) =>
					{
						e.LinkedCommands.Remove(link);
						populateList();
						this.NeedsSave.Value = true;
					};
				}
				foreach (var remove in toRemove)
					e.LinkedCommands.Remove(remove);
			};

			Action addItem = () =>
			{
				if (!addButton.Active) return;
				Entity.CommandLink link = new Entity.CommandLink();
				var entity = ((Entity)destEntityDrop.GetSelectedOption().Related);
				link.TargetEntity = new Entity.Handle() { Target = entity };
				link.TargetCommand = destCommDrop.GetSelectedOption().Text;
				link.SourceCommand = sourceDrop.GetSelectedOption().Text;
				e.LinkedCommands.Add(link);
				populateList();
				addButton.Active.Value = false;
				sourceDrop.LastItemSelected.Value = -1;
				this.NeedsSave.Value = true;
			};
			#endregion

			sourceDrop.Add(new NotifyBinding(() => recompute(true, false, false), sourceDrop.LastItemSelected));
			destEntityDrop.Add(new NotifyBinding(() => recompute(false, true, false), destEntityDrop.LastItemSelected));
			destCommDrop.Add(new NotifyBinding(() => recompute(false, false, true), destCommDrop.LastItemSelected));

			addButton.OnMouseClick += (sender, args) => addItem();


			foreach (var command in GetCommands(e))
			{
				var comm = command.Value as Command;
				var name = command.Key as string;

				sourceDrop.AddOption(name, () => { }, null, comm);
			}

			populateList();

		}

		private IEnumerable<DictionaryEntry> GetCommands(Entity e)
		{
			foreach (var comm in e.Commands)
			{
				var value = comm.Value as Command;
				if (value != null)
					yield return comm;
			}
		}

		private void RecomputePopupCommands()
		{
			this.CreateDropDownView.RemoveAllOptions();
			foreach (var dropDown in PopupCommands)
			{
				string text = dropDown.Description;
				if (dropDown.Chord.Key != Keys.None)
				{
					if (dropDown.Chord.Modifier != Keys.None)
						text += " [" + dropDown.Chord.Modifier.ToString() + "+" + dropDown.Chord.Key.ToString() + "]";
					else
					{
						text += " [" + dropDown.Chord.Key.ToString() + "]";
					}
				}
				CreateDropDownView.AddOption(text, () =>
				{
					dropDown.Action.Execute();
				});
			}
		}

		private void show(Entity entity)
		{
			ActionsPanelView.FindFirstChildByName("LinkerButton").Active.Value = true;
			foreach (DictionaryEntry entry in new DictionaryEntry[] { new DictionaryEntry("[" + entity.Type.ToString() + " entity]", new[] { new DictionaryEntry("ID", entity.ID) }.Concat(entity.Properties).Concat(entity.Commands)) }
				.Union(entity.Components.Where(x => ((IComponent)x.Value).Editable)))
			{
				IEnumerable<DictionaryEntry> properties = null;
				if (typeof(IComponent).IsAssignableFrom(entry.Value.GetType()))
					properties = entry.Value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
						.Select(x => new DictionaryEntry(x.Name, x.GetValue(entry.Value)))
						.Concat(entry.Value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
						.Where(y => y.GetIndexParameters().Length == 0)
						.Select(z => new DictionaryEntry(z.Name, z.GetValue(entry.Value, null))));
				else
					properties = (IEnumerable<DictionaryEntry>)entry.Value;
				properties = properties.Where(x => x.Value != null
					&& ((x.Value.GetType() == typeof(Command) && ((Command)x.Value).ShowInEditor)
					|| (typeof(IProperty).IsAssignableFrom(x.Value.GetType()) && !typeof(IListProperty).IsAssignableFrom(x.Value.GetType()) && (bool)x.Value.GetType().GetProperty("Editable").GetValue(x.Value, null))));

				if (properties.FirstOrDefault().Value == null)
					continue;


				PanelView rootEntityView = new PanelView(main.GeeUI, null, Vector2.Zero);
				rootEntityView.Add(new Binding<int, int>(rootEntityView.Width, i => i, ComponentTabViews.Width));
				rootEntityView.ChildrenLayouts.Add(new VerticalViewLayout(4, true, 6));
				this.ComponentTabViews.AddTab(entry.Key.ToString(), rootEntityView);

				foreach (DictionaryEntry propEntry in properties)
				{
					DictionaryEntry property = propEntry;

					View containerLabel = BuildContainerLabel(property.Key.ToString());
					if (property.Value.GetType() == typeof(Command))
					{
						// It's a command
						//row.Children.Add(this.BuildButton((Command)property.Value, "[Execute]"));
						containerLabel.AddChild(BuildButton((Command)property.Value, "[Execute]"));
					}
					else
					{
						// It's a property
						containerLabel.AddChild(BuildValueView((IProperty)property.Value));
						//row.Children.Add(this.BuildValueField((IProperty)property.Value));
					}
					rootEntityView.AddChild(containerLabel);
				}

				//if (typeof(IEditorGeeUIComponent).IsAssignableFrom(entry.Value.GetType()))
				//((IEditorGeeUIComponent)entry.Value).AddEditorElements(propertyList, this);
			}
		}

		public bool AnyTextFieldViewsSelected()
		{
			var views = main.GeeUI.GetAllViews(main.GeeUI.RootView);
			foreach (var view in views)
			{
				if (!(view is TextFieldView)) continue;
				if (view.Selected.Value) return true;
			}
			return false;
		}

		public View BuildContainerLabel(string label)
		{
			var ret = new View(main.GeeUI, null);
			ret.ChildrenLayouts.Add(new HorizontalViewLayout(6));
			ret.ChildrenLayouts.Add(new ExpandToFitLayout());

			new TextView(main.GeeUI, ret, label, Vector2.Zero, MainFont);
			return ret;
		}

		public View BuildButton(Command command, string label, Color color = default(Color))
		{
			var b = new ButtonView(main.GeeUI, null, label, Vector2.Zero, MainFont);
			b.OnMouseClick += (sender, args) =>
			{
				if (command != null)
					command.Execute();
			};
			//b.ChildrenLayouts.Add(new ExpandToFitLayout());
			return b;
		}
		private void refresh()
		{
			HideLinkerView();
			ActionsPanelView.FindFirstChildByName("LinkerButton").Active.Value = false;
			this.ComponentTabViews.RemoveAllTabs();

			if (this.SelectedEntities.Count == 0 || this.MapEditMode)
				this.show(this.Entity);
			else if (this.SelectedEntities.Count == 1)
				this.show(this.SelectedEntities.First());
			//else
			///this.addText("[" + this.SelectedEntities.Count.ToString() + " entities]"); //TODO: make this do something
		}

		public void BuildValueFieldView(View parent, Type type, IProperty property, VectorElement element, int width = 30)
		{
			TextFieldView textField = new TextFieldView(main.GeeUI, parent, Vector2.Zero, MainFont);
			textField.Height.Value = 15;
			textField.Width.Value = width;
			textField.MultiLine = false;

			if (type.Equals(typeof(Vector2)))
			{
				Property<Vector2> socket = (Property<Vector2>)property;
				textField.Text = socket.Value.GetElement(element).ToString("F");
				socket.AddBinding(new NotifyBinding(() =>
				{
					textField.Text = socket.Value.GetElement(element).ToString("F");
				}, socket));

				Action onChanged = () =>
				{
					float value;
					if (float.TryParse(textField.Text, out value))
					{
						socket.Value = socket.Value.SetElement(element, value);
					}
					textField.Text = socket.Value.GetElement(element).ToString("F");
					textField.Selected.Value = false;
				};
				textField.ValidationRegex = "^\\d+(\\.\\d+)?$";
				textField.OnTextSubmitted = onChanged;
			}
			else if (type.Equals(typeof(Vector3)))
			{
				Property<Vector3> socket = (Property<Vector3>)property;
				textField.Text = socket.Value.GetElement(element).ToString("F");
				socket.AddBinding(new NotifyBinding(() =>
				{
					textField.Text = socket.Value.GetElement(element).ToString("F");
				}, socket));

				Action onChanged = () =>
				{
					float value;
					if (float.TryParse(textField.Text, out value))
					{
						socket.Value = socket.Value.SetElement(element, value);
					}
					textField.Text = socket.Value.GetElement(element).ToString("F");
					textField.Selected.Value = false;
				};
				textField.ValidationRegex = "^\\d+(\\.\\d+)?$";
				textField.OnTextSubmitted = onChanged;
			}
			else if (type.Equals(typeof(Voxel.Coord)))
			{
				Property<Voxel.Coord> socket = (Property<Voxel.Coord>)property;

				Direction dir;
				switch (element)
				{
					case VectorElement.X:
						dir = Direction.PositiveX;
						break;
					case VectorElement.Y:
						dir = Direction.PositiveY;
						break;
					default:
						dir = Direction.PositiveZ;
						break;
				}

				textField.Text = socket.Value.GetComponent(dir).ToString();
				socket.AddBinding(new NotifyBinding(() =>
				{
					textField.Text = socket.Value.GetComponent(dir).ToString();
				}, socket));


				Action onChanged = () =>
				{
					int value;
					if (int.TryParse(textField.Text, out value))
					{
						Voxel.Coord c = socket.Value;
						c.SetComponent(dir, value);
						socket.Value = c;
					}
					textField.Text = socket.Value.GetComponent(dir).ToString();
					textField.Selected.Value = false;
				};
				textField.ValidationRegex = "^\\d+$";
				textField.OnTextSubmitted = onChanged;
			}
			else if (type.Equals(typeof(Vector4)))
			{
				Property<Vector4> socket = (Property<Vector4>)property;
				textField.Text = socket.Value.GetElement(element).ToString("F");
				socket.AddBinding(new NotifyBinding(() =>
				{
					textField.Text = socket.Value.GetElement(element).ToString("F");
				}, socket));

				Action onChanged = () =>
				{
					float value;
					if (float.TryParse(textField.Text, out value))
					{
						socket.Value = socket.Value.SetElement(element, value);
					}
					textField.Text = socket.Value.GetElement(element).ToString("F");
					textField.Selected.Value = false;
				};
				textField.ValidationRegex = "^\\d+(\\.\\d+)?$";
				textField.OnTextSubmitted = onChanged;
			}
			else if (type.Equals(typeof(Quaternion)))
			{
				Property<Quaternion> socket = (Property<Quaternion>)property;
				textField.Text = socket.Value.GetElement(element).ToString("F");
				socket.AddBinding(new NotifyBinding(() =>
				{
					textField.Text = socket.Value.GetElement(element).ToString("F");
				}, socket));

				Action onChanged = () =>
				{
					float value;
					if (float.TryParse(textField.Text, out value))
					{
						socket.Value = socket.Value.SetElement(element, value);
					}
					textField.Text = socket.Value.GetElement(element).ToString("F");
					textField.Selected.Value = false;
				};
				textField.ValidationRegex = "^\\d+(\\.\\d+)?$";
				textField.OnTextSubmitted = onChanged;
			}
			else if (type.Equals(typeof(Color)))
			{
				Property<Color> socket = (Property<Color>)property;
				textField.Text = socket.Value.GetElement(element).ToString("F");
				socket.AddBinding(new NotifyBinding(() =>
				{
					textField.Text = socket.Value.GetElement(element).ToString("F");
				}, socket));

				Action onChanged = () =>
				{
					byte value;
					if (byte.TryParse(textField.Text, out value))
					{
						socket.Value = socket.Value.SetElement(element, value);
					}
					textField.Text = socket.Value.GetElement(element).ToString("F");
					textField.Selected.Value = false;
				};
				textField.ValidationRegex = "^\\d+$";
				textField.OnTextSubmitted = onChanged;
			}
		}

		public View BuildValueView(IProperty property)
		{
			View ret = new View(main.GeeUI, null);
			ret.ChildrenLayouts.Add(new HorizontalViewLayout(4));
			ret.ChildrenLayouts.Add(new ExpandToFitLayout());

			PropertyInfo propertyInfo = property.GetType().GetProperty("Value");
			if (propertyInfo.PropertyType.Equals(typeof(Vector2)))
			{
				foreach (VectorElement field in new[] { VectorElement.X, VectorElement.Y })
					this.BuildValueFieldView(ret, propertyInfo.PropertyType, property, field);
			}
			else if (propertyInfo.PropertyType.Equals(typeof(Vector3)) || propertyInfo.PropertyType.Equals(typeof(Voxel.Coord)))
			{
				foreach (VectorElement field in new[] { VectorElement.X, VectorElement.Y, VectorElement.Z })
					this.BuildValueFieldView(ret, propertyInfo.PropertyType, property, field);
			}
			else if (propertyInfo.PropertyType.Equals(typeof(Vector4)) || propertyInfo.PropertyType.Equals(typeof(Quaternion)) ||
					 propertyInfo.PropertyType.Equals(typeof(Color)))
			{
				foreach (VectorElement field in new[] { VectorElement.X, VectorElement.Y, VectorElement.Z, VectorElement.W })
					this.BuildValueFieldView(ret, propertyInfo.PropertyType, property, field);
			}
			else if (typeof(Enum).IsAssignableFrom(propertyInfo.PropertyType))
			{
				var fields = propertyInfo.PropertyType.GetFields(BindingFlags.Static | BindingFlags.Public);
				int numFields = fields.Length;
				var drop = new DropDownView(main.GeeUI, ret, Vector2.Zero, MainFont);
				drop.AllowRightClickExecute.Value = false;
				for (int i = 0; i < numFields; i++)
				{
					int i1 = i;
					Action onClick = () =>
					{
						propertyInfo.SetValue(property, Enum.ToObject(propertyInfo.PropertyType, i1), null);
					};
					drop.AddOption(fields[i].Name, onClick);
				}
				drop.SetSelectedOption(propertyInfo.GetValue(property, null).ToString(), false);
				drop.Add(new NotifyBinding(() =>
				{
					drop.SetSelectedOption(propertyInfo.GetValue(property, null).ToString(), false);
				}, (IProperty)property));
			}
			else
			{
				TextFieldView view = new TextFieldView(main.GeeUI, ret, Vector2.Zero, MainFont);
				view.Width.Value = 70;
				view.Height.Value = 15;
				view.Text = "abc";
				view.MultiLine = false;

				if (propertyInfo.PropertyType.Equals(typeof(int)))
				{
					Property<int> socket = (Property<int>)property;
					view.Text = socket.Value.ToString();
					socket.AddBinding(new NotifyBinding(() =>
					{
						view.Text = socket.Value.ToString();
					}, socket));
					Action onChanged = () =>
					{
						int value;
						if (int.TryParse(view.Text, out value))
						{
							socket.Value = value;
						}
						view.Text = socket.Value.ToString();
						view.Selected.Value = false;
					};
					view.ValidationRegex = "^\\d+$";
					view.OnTextSubmitted = onChanged;
				}
				else if (propertyInfo.PropertyType.Equals(typeof(float)))
				{
					Property<float> socket = (Property<float>)property;
					view.Text = socket.Value.ToString("F");
					socket.AddBinding(new NotifyBinding(() =>
					{
						view.Text = socket.Value.ToString();
					}, socket));
					Action onChanged = () =>
					{
						float value;
						if (float.TryParse(view.Text, out value))
						{
							socket.Value = value;
						}
						view.Text = socket.Value.ToString("F");
						view.Selected.Value = false;
					};
					view.ValidationRegex = "^\\d+(\\.\\d+)?$";
					view.OnTextSubmitted = onChanged;
				}
				else if (propertyInfo.PropertyType.Equals(typeof(bool)))
				{
					//No need for a textfield!
					ret.RemoveChild(view);
					CheckBoxView checkBox = new CheckBoxView(main.GeeUI, ret, Vector2.Zero, "", MainFont);
					Property<bool> socket = (Property<bool>)property;
					checkBox.IsChecked.Value = socket.Value;
					checkBox.Add(new NotifyBinding(() =>
					{
						this.NeedsSave.Value = true;
						socket.Value = checkBox.IsChecked.Value;
					}, checkBox.IsChecked));
				}
				else if (propertyInfo.PropertyType.Equals(typeof(string)))
				{
					Property<string> socket = (Property<string>)property;

					if (socket.Value == null) view.Text = "";
					else view.Text = socket.Value;

					socket.AddBinding(new NotifyBinding(() =>
					{
						var text = socket.Value;
						if (text == null) text = "";
						view.Text = text;
					}, socket));

					//Vast majority of strings won't be multiline.
					if (socket.Value != null)
						view.MultiLine = socket.Value.Contains("\n");
					Action onChanged = () =>
					{
						if (socket.Value != view.Text)
							socket.Value = view.Text;
						view.Selected.Value = false;
					};
					view.OnTextSubmitted = onChanged;
				}
			}
			return ret;
		}

		public override void delete()
		{
			base.delete();
			RootEditorView.ParentView.RemoveChild(RootEditorView);
		}
	}
}