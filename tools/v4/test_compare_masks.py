"""The UI metric excludes animated backgrounds while retaining their painted children."""
import unittest

from compare_states import ui_mask


class UiMaskTests(unittest.TestCase):
    def test_transparent_auth_layout_does_not_count_the_planet_as_ui(self):
        layout = {"items": [
            {"id": "auth.side", "visible": True, "box": {"x": 0, "y": 0, "w": 620, "h": 720}},
            {"id": "auth.side.logo", "visible": True, "box": {"x": 162, "y": 150, "w": 296, "h": 69}},
        ]}
        mask = ui_mask(layout, (1280, 720))
        self.assertEqual(mask.getpixel((20, 700)), 0, "EK-001: the transparent layout shows animated stars/planet")
        self.assertEqual(mask.getpixel((170, 160)), 255, "The logo remains part of the UI comparison")

    def test_form_panels_remain_measured_and_build_watermark_is_excluded(self):
        layout = {"items": [
            {"id": "form.panel", "visible": True, "box": {"x": 740, "y": 46, "w": 470, "h": 628}},
            {"id": "test.footer", "visible": True, "box": {"x": 1150, "y": 696, "w": 130, "h": 24}},
        ]}
        mask = ui_mask(layout, (1280, 720))
        self.assertEqual(mask.getpixel((800, 100)), 255)
        self.assertEqual(mask.getpixel((1200, 710)), 0)


if __name__ == "__main__":
    unittest.main()
