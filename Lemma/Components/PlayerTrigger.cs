﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Lemma.Util;
using System.Xml.Serialization;
using Lemma.Factories;
using ComponentBind;

namespace Lemma.Components
{
	public class PlayerTrigger : Component<Main>, IUpdateableComponent
	{
		public EditorProperty<float> Radius = new EditorProperty<float> { Value = 10.0f };
		public Property<Vector3> Position = new Property<Vector3>();
		public Property<bool> IsTriggered = new Property<bool>();
		public Property<Entity.Handle> Player = new Property<Entity.Handle>();

		[XmlIgnore]
		public Command PlayerEntered = new Command();

		[XmlIgnore]
		public Command PlayerExited = new Command();

		public PlayerTrigger()
		{
			this.EnabledInEditMode = false;
			this.EnabledWhenPaused = false;
			this.Enabled.Editable = true;
		}

		public override void Awake()
		{
			base.Awake();
			Action clear = delegate()
			{
				this.IsTriggered.Value = false;
				this.Player.Value = null;
			};
			this.Add(new CommandBinding(this.OnSuspended, clear));
			this.Add(new CommandBinding(this.OnDisabled, clear));
		}

		public void Update(float elapsedTime)
		{
			bool playerFound = false;
			Entity player = PlayerFactory.Instance;
			if (player != null && (player.Get<Transform>().Position.Value - this.Position.Value).Length() <= this.Radius)
			{
				playerFound = true;
				if (!this.IsTriggered)
				{
					this.Player.Value = player;
					this.IsTriggered.Value = true;
					this.PlayerEntered.Execute();
				}
			}

			if (!playerFound && this.IsTriggered)
			{
				this.PlayerExited.Execute();
				this.IsTriggered.Value = false;
				this.Player.Value = null;
			}
		}

		public static void AttachEditorComponents(Entity entity, Main main, Vector3 color)
		{
			Transform transform = entity.Get<Transform>();

			ModelAlpha model = new ModelAlpha();
			model.Filename.Value = "Models\\alpha-sphere";
			model.Alpha.Value = 0.15f;
			model.Color.Value = color;
			model.DisableCulling.Value = true;
			PlayerTrigger trigger = entity.Get<PlayerTrigger>();
			model.Add(new Binding<Vector3, float>(model.Scale, x => new Vector3(x), trigger.Radius));
			model.Editable = false;
			model.Serialize = false;
			model.DrawOrder.Value = 11; // In front of water
			model.Add(new Binding<bool>(model.Enabled, () => trigger.Enabled && entity.EditorSelected, trigger.Enabled, entity.EditorSelected));

			entity.Add(model);

			model.Add(new Binding<Matrix, Vector3>(model.Transform, x => Matrix.CreateTranslation(x), transform.Position));
		}
	}
}
