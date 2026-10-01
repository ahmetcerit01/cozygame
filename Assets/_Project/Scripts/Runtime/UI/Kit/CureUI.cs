using System;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// The Cure Cells UI kit: builders for the shared, reusable components every screen is made of.
    /// Visual values come from <see cref="CureTokens"/>; interaction from <see cref="PressFeedback"/>;
    /// every label is a localization key. Screens compose these instead of styling things themselves.
    ///
    ///   PrimaryButton · SecondaryButton · NavigationCard · ContentCard · CurrencyPill · IconButton
    ///   BackButton · SettingsButton · Badge · Header · ProgressBar · Modal · RewardCard
    /// </summary>
    public static class CureUI
    {
        /// <summary>Authored UI icons (settings, coin, research, "+"...), assigned once at startup by the flow.</summary>
        public static HomeIcons Icons { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Icons = default;

        public enum ButtonStyle
        {
            Primary,    // teal: the one main action on a screen
            Secondary,  // cream: supporting actions
        }

        // ---------------------------------------------------------------- buttons

        /// <summary>Glossy teal button: the one main action on a screen (CONTINUE, PLAY, UPGRADE...).</summary>
        public static Button PrimaryButton(string name, Transform parent, string labelKey, Vector2 size, Vector2 position,
            int fontSize = CureTokens.ButtonSize) =>
            GlossyButton(name, parent, labelKey, size, position, fontSize, ButtonStyle.Primary);

        /// <summary>Glossy cream button for supporting actions (2× REWARD, LEVELS...).</summary>
        public static Button SecondaryButton(string name, Transform parent, string labelKey, Vector2 size, Vector2 position,
            int fontSize = CureTokens.ButtonSizeSmall) =>
            GlossyButton(name, parent, labelKey, size, position, fontSize, ButtonStyle.Secondary);

        /// <summary>
        /// Rounded glossy button: drop shadow, darker base it presses onto, light rim, glossy upper half, an inset
        /// plate behind the label and a specular line. Primary buttons also get a periodic shine sweep.
        /// </summary>
        public static Button GlossyButton(string name, Transform parent, string labelKey, Vector2 size, Vector2 position, int fontSize,
            ButtonStyle style)
        {
            bool primary = style == ButtonStyle.Primary;
            Color baseColor = primary ? CureTokens.TealBase : CureTokens.Caramel;
            Color rim = primary ? CureTokens.TealRim : CureTokens.CreamLight;
            Color body = primary ? CureTokens.Teal : CureTokens.Cream;
            Color gloss = primary ? CureTokens.TealGloss : CureTokens.CreamLight;
            Color shade = primary ? new Color(0.08f, 0.38f, 0.34f, 0.55f) : new Color(0.66f, 0.47f, 0.29f, 0.25f);
            Color plate = primary ? new Color(0.07f, 0.35f, 0.31f, 0.32f) : new Color(0.66f, 0.47f, 0.29f, 0.12f);

            float r = size.y * 0.5f;
            float depth = Mathf.Round(Mathf.Clamp(size.y * 0.087f, 7f, 13f));
            var rt = UIFactory.CreateCentered(name, parent, size, position);
            if (primary)
                UIFactory.CreateSpriteImage("Glow", rt, ProceduralSprites.SoftGlow, new Vector2(size.x * 1.25f, size.y * 1.9f),
                    new Color(0.45f, 0.9f, 0.8f, 0.22f), new Vector2(0f, -4f));
            var shadow = UIFactory.CreateShadow("Shadow", rt, size, 28f, new Color(0.12f, 0.06f, 0.02f, 0.55f), new Vector2(0f, -depth - 10f));
            UIFactory.CreateRoundedImage("Base", rt, size, r, baseColor, new Vector2(0f, -depth));

            var face = UIFactory.CreateCentered("Face", rt, size);
            var rimImage = UIFactory.CreateRoundedImage("Rim", face, size, r, rim);
            var inner = size - new Vector2(10f, 10f);
            var bodyImage = UIFactory.CreateRoundedImage("Body", face, inner, inner.y * 0.5f, body);
            bodyImage.raycastTarget = true;
            bodyImage.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var clip = bodyImage.transform;

            UIFactory.CreateRoundedImage("TopGloss", clip, new Vector2(inner.x, inner.y * 0.58f), inner.y * 0.29f,
                primary ? gloss : new Color(gloss.r, gloss.g, gloss.b, 0.85f), new Vector2(0f, inner.y * 0.21f));
            UIFactory.CreateRoundedImage("BottomShade", clip, new Vector2(inner.x - 20f, inner.y * 0.28f), inner.y * 0.14f, shade,
                new Vector2(0f, -inner.y * 0.36f));
            UIFactory.CreateRoundedImage("Plate", clip, new Vector2(size.x * 0.6f, size.y * 0.52f), size.y * 0.26f, plate, new Vector2(0f, -2f));
            UIFactory.CreateRoundedImage("Specular", clip, new Vector2(Mathf.Max(20f, inner.x - r * 1.3f), 10f), 5f,
                new Color(1f, 1f, 1f, 0.6f), new Vector2(0f, inner.y * 0.5f - 14f));
            UIFactory.CreateSpriteImage("SpecDot", clip, ProceduralSprites.SoftGlow, new Vector2(size.y * 0.6f, size.y * 0.22f),
                new Color(1f, 1f, 1f, 0.8f), new Vector2(-inner.x * 0.36f, inner.y * 0.26f));

            if (primary)
            {
                var sweep = UIFactory.CreateCentered("Shine", clip, new Vector2(46f, size.y * 2.2f), new Vector2(-size.x, 0f));
                sweep.localRotation = Quaternion.Euler(0f, 0f, -24f);
                UIFactory.AddImage(sweep, new Color(1f, 1f, 1f, 0.28f));
                rt.gameObject.AddComponent<ShineSweep>().Configure(sweep, size.x * 0.62f, 3.8f);
            }

            var text = UIFactory.CreateText("Label", clip, string.Empty, fontSize, primary ? CureTokens.TextOnTeal : CureTokens.Text,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(24f, 0f);
            text.rectTransform.offsetMax = new Vector2(-24f, 0f);
            FitLabel(text, fontSize);
            if (primary)
            {
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = CureTokens.TealInk;
                outline.effectDistance = new Vector2(2f, -2f);
                var drop = text.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0.05f, 0.25f, 0.22f, 0.55f);
                drop.effectDistance = new Vector2(0f, -4f);
            }
            else
            {
                Emboss(text);
            }
            if (!string.IsNullOrEmpty(labelKey)) text.Localize(labelKey);

            return Pressable(rt, face, depth, shadow, bodyImage, rimImage);
        }

        // ---------------------------------------------------------------- cards

        /// <summary>
        /// Beveled cream navigation card: warm drop shadow, caramel base it presses onto, golden rim, cream face
        /// with a soft top highlight, an authored icon (with contact shadow) above an embossed label.
        /// </summary>
        public static Button NavigationCard(string name, Transform parent, string labelKey, Vector2 size, Vector2 position,
            Sprite icon, float iconSize, int fontSize = 38)
        {
            const float radius = CureTokens.RadiusCard;
            const float depth = CureTokens.Bevel;
            var rt = UIFactory.CreateCentered(name, parent, size, position);
            var shadow = UIFactory.CreateShadow("Shadow", rt, size, 26f, CureTokens.Shadow, new Vector2(0f, -depth - 8f));
            UIFactory.CreateRoundedImage("Base", rt, size, radius, CureTokens.Caramel, new Vector2(0f, -depth));

            var face = UIFactory.CreateCentered("Face", rt, size);
            var rim = UIFactory.CreateRoundedImage("Rim", face, size, radius, CureTokens.CreamRim);
            var inner = size - new Vector2(12f, 12f);
            var body = UIFactory.CreateRoundedImage("Body", face, inner, radius - 6f, CureTokens.Cream);
            body.raycastTarget = true;
            UIFactory.CreateRoundedImage("TopGloss", face, new Vector2(inner.x - 12f, inner.y * 0.54f), radius - 12f,
                new Color(1f, 0.97f, 0.9f, 0.55f), new Vector2(0f, inner.y * 0.2f));
            UIFactory.CreateRoundedImage("Specular", face, new Vector2(inner.x - 90f, 8f), 4f, new Color(1f, 1f, 1f, 0.85f),
                new Vector2(0f, size.y * 0.5f - 13f));

            float labelHeight = fontSize + 10f;
            float labelY = -size.y * 0.5f + 20f + labelHeight * 0.5f;
            float iconTop = size.y * 0.5f - 16f;
            float iconBottom = labelY + labelHeight * 0.5f - 2f;
            // Authored icons carry ~10% transparent padding, so they may use slightly more than the free band.
            float fit = Mathf.Min(iconSize, (iconTop - iconBottom) * 1.12f);
            float iconY = (iconTop + iconBottom) * 0.5f + 6f;
            UIFactory.CreateSpriteImage("IconShadow", face, ProceduralSprites.SoftGlow, new Vector2(fit * 0.9f, fit * 0.24f),
                new Color(0.3f, 0.16f, 0.05f, 0.35f), new Vector2(0f, iconY - fit * 0.42f));
            SpriteIcon(face, "Icon", icon, fit, new Vector2(0f, iconY));

            var text = UIFactory.CreateText("Label", face, string.Empty, fontSize, CureTokens.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaStyle.Place(text.rectTransform, new Vector2(size.x - 24f, labelHeight), new Vector2(0f, labelY));
            FitLabel(text, fontSize);
            Emboss(text);
            if (!string.IsNullOrEmpty(labelKey)) text.Localize(labelKey);

            return Pressable(rt, face, depth, shadow, body, rim);
        }

        /// <summary>
        /// Raised cream content card (not interactive): drop shadow, caramel bevel, golden rim and a parchment face
        /// with a soft top highlight. Returns the face, which callers fill.
        /// </summary>
        public static RectTransform ContentCard(string name, Transform parent, Vector2 size, Vector2 position,
            float radius = CureTokens.RadiusCard)
        {
            var rt = UIFactory.CreateCentered(name, parent, size, position);
            UIFactory.CreateShadow("Shadow", rt, size, 30f, CureTokens.Shadow, new Vector2(0f, -CureTokens.Bevel - 10f));
            UIFactory.CreateRoundedImage("Base", rt, size, radius, CureTokens.Caramel, new Vector2(0f, -CureTokens.Bevel));
            UIFactory.CreateRoundedImage("Rim", rt, size, radius, CureTokens.CreamRim);
            var inner = size - new Vector2(CureTokens.RimInset * 2f, CureTokens.RimInset * 2f);
            var face = UIFactory.CreateRoundedImage("Face", rt, inner, radius - CureTokens.RimInset, CureTokens.Parchment);
            UIFactory.CreateRoundedImage("TopGloss", face.transform, new Vector2(inner.x - 16f, Mathf.Min(inner.y * 0.4f, 160f)),
                Mathf.Max(8f, radius - 14f), new Color(1f, 0.98f, 0.93f, 0.6f), new Vector2(0f, inner.y * 0.5f - Mathf.Min(inner.y * 0.2f, 80f) - 8f));
            UIFactory.CreateRoundedImage("Specular", face.transform, new Vector2(inner.x - radius * 2f, 8f), 4f,
                new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, inner.y * 0.5f - 8f));
            return face.rectTransform;
        }

        // ---------------------------------------------------------------- small controls

        public static CurrencyPill CurrencyPill(string name, Transform parent, float width, Vector2 anchor, Vector2 position,
            Sprite icon, Sprite plusIcon)
        {
            var rt = UIFactory.CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            var pill = rt.gameObject.AddComponent<CurrencyPill>();
            pill.Build(name, width, icon, plusIcon);
            return pill;
        }

        /// <summary>Round warm-glass icon button with an authored sprite.</summary>
        public static Button IconButton(string name, Transform parent, float size, Sprite icon, float iconScale = 0.72f) =>
            IconButton(name, parent, size, (rt, s) => SpriteIcon(rt, "Icon", icon, s, Vector2.zero), iconScale);

        /// <summary>Round warm-glass icon button with a procedural glyph (drawn in dark brown).</summary>
        public static Button IconButton(string name, Transform parent, float size, Action<RectTransform, float, Color> glyph,
            float iconScale = 0.46f) =>
            IconButton(name, parent, size, (rt, s) => glyph(rt, s, CureTokens.Text), iconScale);

        private static Button IconButton(string name, Transform parent, float size, Action<RectTransform, float> drawIcon, float iconScale)
        {
            var rt = UIFactory.CreateCentered(name, parent, new Vector2(size, size));
            var parts = GlassBody(rt, new Vector2(size, size), round: true);
            var iconRt = UIFactory.CreateCentered("Glyph", rt, new Vector2(size, size) * iconScale);
            drawIcon(iconRt, size * iconScale);
            var hit = parts.Rim;
            hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            rt.gameObject.AddComponent<PressFeedback>().Configure(null, 0f, parts.Shadow, parts.Body);
            return button;
        }

        public static Button BackButton(Transform parent, float size = 96f) => IconButton("BackButton", parent, size, MetaIcons.ArrowLeft);

        public static Button SettingsButton(Transform parent, float size = 92f) => IconButton("SettingsButton", parent, size, Icons.settings);

        /// <summary>Coral notification badge ("!" or a count), hidden until shown.</summary>
        public static NotificationBadge Badge(Transform parent, float size, Vector2 position, int count = 0)
        {
            var rt = UIFactory.CreateCentered("Badge", parent, new Vector2(size, size), position);
            var badge = rt.gameObject.AddComponent<NotificationBadge>();
            badge.Build(size);
            badge.SetCount(count);
            rt.gameObject.SetActive(false);
            return badge;
        }

        // ---------------------------------------------------------------- headers, text

        /// <summary>
        /// Screen header anchored to the top of <paramref name="safe"/>: an optional back button on the left and a
        /// light title with a warm outline and drop shadow, readable over the authored artwork.
        /// </summary>
        public static ScreenHeader Header(Transform safe, string titleKey, bool withBack, float height = 150f)
        {
            var band = MetaStyle.TopBand(safe, height);
            var header = band.gameObject.AddComponent<ScreenHeader>();
            var title = UIFactory.CreateText("Title", band, string.Empty, CureTokens.TitleSize, CureTokens.TitleLight,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(150f, 0f);
            title.rectTransform.offsetMax = new Vector2(-150f, 0f);
            FitLabel(title, CureTokens.TitleSize);
            TitleEffects(title, 3f);
            title.Localize(titleKey);
            header.Title = title;
            if (withBack)
            {
                header.Back = BackButton(band);
                var backRt = (RectTransform)header.Back.transform;
                backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 0.5f);
                backRt.anchoredPosition = new Vector2(36f + 48f, 0f);
            }
            return header;
        }

        /// <summary>Dark-brown body text, bound to a key.</summary>
        public static Text BodyText(string name, Transform parent, string key, int size = CureTokens.BodySize,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = UIFactory.CreateText(name, parent, string.Empty, size, CureTokens.Text, alignment);
            if (!string.IsNullOrEmpty(key)) text.Localize(key);
            return text;
        }

        public static CureProgressBar ProgressBar(Transform parent, Vector2 size, Vector2 position, bool withLabel = true)
        {
            var rt = UIFactory.CreateCentered("ProgressBar", parent, size, position);
            var bar = rt.gameObject.AddComponent<CureProgressBar>();
            bar.Build(size, withLabel);
            return bar;
        }

        /// <summary>
        /// A modal panel on its own canvas layer (above screens and the tab bar). Fill <see cref="ModalPanel.Content"/>.
        /// </summary>
        public static ModalPanel Modal(string name, Transform canvasRoot, Vector2 size, string titleKey, int sortingOrder = 30)
        {
            var rt = UIFactory.CreateStretched(name, canvasRoot);
            MetaStyle.RaiseSorting(rt.gameObject, sortingOrder);
            var modal = rt.gameObject.AddComponent<ModalPanel>();
            modal.Build(size);
            if (!string.IsNullOrEmpty(titleKey)) modal.SetTitle(titleKey);
            return modal;
        }

        public static RewardCard RewardCard(Transform parent, Vector2 size, Vector2 position, Sprite icon, string labelKey)
        {
            var rt = UIFactory.CreateCentered("RewardCard", parent, size, position);
            var card = rt.gameObject.AddComponent<RewardCard>();
            card.Build(size, icon, labelKey);
            return card;
        }

        // ---------------------------------------------------------------- shared pieces

        public struct GlassParts
        {
            public Image Shadow;
            public Image Rim;
            public Image Body;
        }

        /// <summary>Warm glass body with a dark caramel rim (pills and round buttons share it).</summary>
        public static GlassParts GlassBody(Transform parent, Vector2 size, bool round)
        {
            var parts = new GlassParts();
            float r = size.y * 0.5f;
            if (round)
            {
                parts.Shadow = UIFactory.CreateSpriteImage("Shadow", parent, ProceduralSprites.SoftGlow, size * 1.35f,
                    new Color(0.12f, 0.06f, 0.02f, 0.5f), new Vector2(0f, -8f));
                parts.Rim = UIFactory.CreateSpriteImage("Rim", parent, ProceduralSprites.Circle, size,
                    new Color(CureTokens.CaramelDark.r, CureTokens.CaramelDark.g, CureTokens.CaramelDark.b, 0.9f));
                parts.Body = UIFactory.CreateSpriteImage("Body", parent, ProceduralSprites.Circle, size - new Vector2(8f, 8f),
                    new Color(1f, 0.95f, 0.86f, 0.93f));
                UIFactory.CreateSpriteImage("Shade", parent, ProceduralSprites.Circle, size * 0.62f,
                    new Color(0.91f, 0.8f, 0.63f, 0.45f), new Vector2(0f, -size.y * 0.14f));
                UIFactory.CreateSpriteImage("Gloss", parent, ProceduralSprites.SoftGlow, new Vector2(size.x * 0.6f, size.y * 0.3f),
                    new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, size.y * 0.26f));
                return parts;
            }
            parts.Shadow = UIFactory.CreateShadow("Shadow", parent, size, 16f, new Color(0.12f, 0.06f, 0.02f, 0.45f), new Vector2(0f, -6f));
            parts.Rim = UIFactory.CreateRoundedImage("Rim", parent, size, r,
                new Color(CureTokens.CaramelDark.r, CureTokens.CaramelDark.g, CureTokens.CaramelDark.b, 0.88f));
            var inner = size - new Vector2(8f, 8f);
            parts.Body = UIFactory.CreateRoundedImage("Body", parent, inner, inner.y * 0.5f, new Color(1f, 0.95f, 0.86f, 0.93f));
            UIFactory.CreateRoundedImage("Shade", parent, new Vector2(inner.x - 10f, inner.y * 0.46f), inner.y * 0.23f,
                new Color(0.91f, 0.8f, 0.63f, 0.55f), new Vector2(0f, -inner.y * 0.25f));
            UIFactory.CreateRoundedImage("Gloss", parent, new Vector2(inner.x - 46f, 11f), 5.5f, new Color(1f, 1f, 1f, 0.8f),
                new Vector2(0f, inner.y * 0.5f - 11f));
            return parts;
        }

        /// <summary>Authored icon image (aspect preserved; hidden when the sprite is missing).</summary>
        public static Image SpriteIcon(Transform parent, string name, Sprite sprite, float size, Vector2 position)
        {
            var image = UIFactory.CreateSpriteImage(name, parent, sprite, new Vector2(size, size), Color.white, position);
            image.preserveAspect = true;
            image.enabled = sprite != null;
            return image;
        }

        /// <summary>Light 1-px highlight under dark text: reads as engraved into cream.</summary>
        public static void Emboss(Text text)
        {
            var emboss = text.gameObject.AddComponent<Shadow>();
            emboss.effectColor = new Color(1f, 1f, 1f, 0.6f);
            emboss.effectDistance = new Vector2(0f, -2f);
        }

        /// <summary>Warm outline + drop shadow for light titles over artwork.</summary>
        public static void TitleEffects(Text text, float weight)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.29f, 0.17f, 0.08f, 0.95f);
            outline.effectDistance = new Vector2(weight, -weight);
            var drop = text.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0.15f, 0.08f, 0.03f, 0.6f);
            drop.effectDistance = new Vector2(0f, -weight - 2f);
        }

        /// <summary>Lets a label shrink (never grow) to fit longer translations on one line.</summary>
        public static void FitLabel(Text text, int maxSize)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = maxSize;
            text.resizeTextMinSize = Mathf.Max(14, Mathf.RoundToInt(maxSize * 0.6f));
        }

        private static Button Pressable(RectTransform rt, RectTransform face, float depth, Image shadow, params Graphic[] tinted)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = tinted.Length > 0 ? tinted[0] : null;
            button.transition = Selectable.Transition.None; // PressFeedback tints instead
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            rt.gameObject.AddComponent<PressFeedback>().Configure(face, depth * 0.65f, shadow, tinted);
            return button;
        }
    }
}
