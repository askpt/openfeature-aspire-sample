import sys
import os
import json

sys.path.insert(0, "/system/apps/ofrep")
os.chdir("/system/apps/ofrep")

from badgeware import (
    screen, PixelFont, brushes, shapes, io, run, Image, SpriteSheet, Matrix,
)
import network
import gc

# -- Fonts & Colors --------------------------------------------------------

small_font = PixelFont.load("/system/assets/fonts/ark.ppf")
large_font = PixelFont.load("/system/assets/fonts/absolute.ppf")

white = brushes.color(235, 245, 255)
faded = brushes.color(235, 245, 255, 100)
phosphor = brushes.color(211, 250, 55, 150)
green = brushes.color(60, 200, 80)
red = brushes.color(200, 60, 60)
dark_bg = brushes.color(15, 20, 30)
bar_bg = brushes.color(40, 50, 70)
header_bg = brushes.color(25, 35, 55)

# -- State -----------------------------------------------------------------

INIT = 0
CONNECTING = 1
IDLE = 2
EVALUATING = 3
DISPLAY = 4
ERROR = 5
NO_SECRETS = 6
WIFI_ERROR = 7
RAW_DISPLAY = 8

state = INIT
wlan = None
ticks_start = None
client = None
eval_task = None
WIFI_TIMEOUT = 30

# Demo flag keys — edit to match your OFREP server
FLAG_KEYS = [
    "enable-chatbot",
    "enable-database-winners",
    "enable-stats-header",
    "enable-tabs",
    "enable-database-winners"
]

flag_index = 0
cache = {}
error_message = ""
mona = None
raw_scroll = 0
openfeature_logo = None

# -- Secrets ---------------------------------------------------------------


def load_secrets():
    global client, mona, openfeature_logo
    try:
        sys.path.insert(0, "/")
        from secrets import WIFI_SSID, WIFI_PASSWORD
        try:
            from secrets import GITHUB_USERNAME
        except ImportError:
            GITHUB_USERNAME = ""
        try:
            from secrets import OFREP_BASE_URL, OFREP_BEARER_TOKEN
        except ImportError:
            OFREP_BASE_URL = ""
            OFREP_BEARER_TOKEN = ""
        sys.path.pop(0)
    except ImportError:
        sys.path.pop(0)
        return None

    if not WIFI_SSID or not OFREP_BASE_URL:
        return None

    from ofrep_client import OFREPClient

    targeting = "badge-%s" % GITHUB_USERNAME if GITHUB_USERNAME else "badge-user"
    client = OFREPClient(OFREP_BASE_URL, OFREP_BEARER_TOKEN)

    mona = SpriteSheet("/system/assets/mona-sprites/mona-default.png", 11, 1)
    try:
        openfeature_logo = Image.load("icon.png")
    except Exception:
        openfeature_logo = None

    return {
        "ssid": WIFI_SSID,
        "password": WIFI_PASSWORD,
        "targeting": targeting,
    }


secrets = None

# -- WiFi ------------------------------------------------------------------


def wifi_connect(ssid, password):
    global wlan, ticks_start

    if ticks_start is None:
        ticks_start = io.ticks

    if wlan is None:
        wlan = network.WLAN(network.STA_IF)
        wlan.active(True)
        if wlan.isconnected():
            return True
        wlan.connect(ssid, password)

    if wlan.isconnected():
        return True

    if io.ticks - ticks_start > WIFI_TIMEOUT * 1000:
        return False

    return None  # still connecting


def wifi_reset():
    global wlan, ticks_start
    if wlan is not None:
        try:
            wlan.disconnect()
            wlan.active(False)
        except Exception:
            pass
    wlan = None
    ticks_start = None


# -- Flag Evaluation (generator-based, non-blocking) ----------------------


def evaluate_flag_gen(key):
    """Generator that yields once to allow a frame render, then evaluates."""
    yield  # allow the EVALUATING spinner to render one frame

    if client is None:
        yield _make_error("CLIENT_ERROR", "Client not initialized")
        return

    context = {"targetingKey": secrets["targeting"]}
    result = client.evaluate_flag(key, context)
    yield result


def _make_error(code, message):
    return {
        "key": FLAG_KEYS[flag_index],
        "value": None,
        "value_type": None,
        "reason": "ERROR",
        "variant": "",
        "metadata": {},
        "request_context": {"targetingKey": secrets["targeting"]} if secrets else {},
        "ofrep_response": None,
        "http_status": None,
        "error": {"code": code, "message": message},
    }


# -- Drawing Helpers -------------------------------------------------------


def center_text(text, y):
    w, _ = screen.measure_text(text)
    screen.text(text, 80 - (w / 2), y)


def draw_header():
    screen.brush = header_bg
    screen.draw(shapes.rectangle(0, 0, 160, 18))

    if openfeature_logo:
        screen.blit(openfeature_logo, 2, 1)

    screen.font = small_font
    screen.brush = phosphor
    center_text("OFREP Demo", 3)

    if mona:
        frame = mona.sprite(int(io.ticks / 200) % 11, 0)
        frame.alpha = 180
        screen.blit(frame, 140, 1)


def draw_flag_selector():
    key = FLAG_KEYS[flag_index]
    screen.font = small_font
    screen.brush = faded
    center_text("< %d/%d >" % (flag_index + 1, len(FLAG_KEYS)), 21)
    screen.brush = white
    screen.font = large_font
    display_key = key if len(key) <= 18 else key[:15] + "..."
    center_text(display_key, 31)


def draw_boolean_value(value):
    cx, cy = 80, 65
    if value:
        screen.brush = green
        screen.draw(shapes.circle(cx - 25, cy, 10))
        screen.brush = white
        screen.font = large_font
        screen.text("ON", cx - 5, cy - 7)
    else:
        screen.brush = red
        screen.draw(shapes.circle(cx - 25, cy, 10))
        screen.brush = white
        screen.font = large_font
        screen.text("OFF", cx - 8, cy - 7)


def draw_string_value(value):
    screen.font = large_font
    screen.brush = phosphor
    s = str(value)
    display = s if len(s) <= 16 else s[:13] + "..."
    center_text(display, 58)


def draw_number_value(value, value_type):
    screen.font = large_font
    screen.brush = white
    center_text(str(value), 55)

    bar_x, bar_y, bar_w, bar_h = 20, 72, 120, 8
    screen.brush = bar_bg
    screen.draw(shapes.rounded_rectangle(bar_x, bar_y, bar_w, bar_h, 3))

    fill_pct = max(0, min(100, float(value))) / 100.0
    fill_w = int(bar_w * fill_pct)
    if fill_w > 0:
        screen.brush = phosphor
        screen.draw(shapes.rounded_rectangle(bar_x, bar_y, fill_w, bar_h, 3))


def draw_value(result):
    value = result["value"]
    vtype = result["value_type"]

    if vtype == "boolean":
        draw_boolean_value(value)
    elif vtype == "string":
        draw_string_value(value)
    elif vtype in ("integer", "float"):
        draw_number_value(value, vtype)
    else:
        screen.font = small_font
        screen.brush = faded
        center_text(str(value), 60)

    screen.font = small_font
    screen.brush = faded
    meta = ""
    if result.get("variant"):
        meta = "variant: %s" % result["variant"]
    if result.get("reason") and result["reason"] != "UNKNOWN":
        sep = " | " if meta else ""
        meta += "%s%s" % (sep, result["reason"])
    if meta:
        center_text(meta, 90)


def draw_button_hints():
    screen.font = small_font
    screen.brush = faded
    screen.text("A:eval", 4, 110)
    w, _ = screen.measure_text("C:refresh")
    screen.text("C:refresh", 156 - w, 110)


def _wrap_text(text, width):
    if text is None:
        return [""]

    screen.font = small_font
    out = []
    lines = str(text).split("\n")
    for line in lines:
        if not line:
            out.append("")
            continue

        remaining = line
        while remaining:
            end = 1
            while end <= len(remaining):
                chunk = remaining[:end]
                chunk_width, _ = screen.measure_text(chunk)
                if chunk_width > width:
                    break
                end += 1

            if end == 1:
                out.append(remaining[:1])
                remaining = remaining[1:]
                continue

            fitted = remaining[: end - 1]
            out.append(fitted)
            remaining = remaining[end - 1 :]
    return out


def _format_detail_value(value):
    if value is None:
        return "null"
    if isinstance(value, dict) or isinstance(value, list):
        try:
            return json.dumps(value)
        except Exception:
            return str(value)
    return str(value)


def _build_detail_lines(result):
    lines = []
    lines.append("flag:%s" % result.get("key", FLAG_KEYS[flag_index]))

    status = result.get("http_status")
    if status is not None:
        lines.append("http:%s" % status)

    value_type = result.get("value_type")
    if value_type:
        lines.append("type:%s" % value_type)

    if result.get("error"):
        lines.append("error:%s" % result["error"].get("code", ""))
        lines.append("msg:%s" % result["error"].get("message", ""))
    else:
        lines.append("value:%s" % _format_detail_value(result.get("value")))

    reason = result.get("reason")
    if reason:
        lines.append("reason:%s" % reason)

    variant = result.get("variant")
    if variant:
        lines.append("variant:%s" % variant)

    context = result.get("request_context")
    if context:
        lines.append("ctx:%s" % _format_detail_value(context))

    metadata = result.get("metadata")
    if metadata:
        lines.append("meta:%s" % _format_detail_value(metadata))

    payload = result.get("ofrep_response")
    if payload is not None:
        lines.append("resp:%s" % _format_detail_value(payload))

    out = []
    for line in lines:
        out.extend(_wrap_text(line, 152))
    return out


def draw_raw_response_screen():
    screen.brush = dark_bg
    screen.draw(shapes.rectangle(0, 0, 160, 120))
    draw_header()
    draw_flag_selector()

    key = FLAG_KEYS[flag_index]
    result = cache.get(key)
    if not result:
        screen.font = small_font
        screen.brush = faded
        center_text("No cached result", 60)
        return

    lines = _build_detail_lines(result)
    max_visible = 7
    max_scroll = max(0, len(lines) - max_visible)
    start = max(0, min(raw_scroll, max_scroll))
    visible = lines[start : start + max_visible]

    screen.font = small_font
    screen.brush = phosphor
    y = 44
    for line in visible:
        screen.text(line, 4, y)
        y += 9

    screen.brush = faded
    center_text("B:back  UP/DN:scroll", 108)


# -- Screens ---------------------------------------------------------------


def draw_no_secrets():
    screen.brush = dark_bg
    screen.draw(shapes.rectangle(0, 0, 160, 120))
    draw_header()

    screen.font = large_font
    screen.brush = white
    center_text("Setup Needed!", 25)

    screen.brush = phosphor
    screen.font = small_font
    screen.text("1:", 10, 45)
    screen.text("Edit secrets.py", 30, 45)
    screen.text("2:", 10, 60)
    screen.text("Set OFREP_BASE_URL", 30, 60)
    screen.text("3:", 10, 75)
    screen.text("Set OFREP_BEARER_TOKEN", 30, 75)
    screen.text("4:", 10, 90)
    screen.text("Set WiFi credentials", 30, 90)


def draw_connecting():
    screen.brush = dark_bg
    screen.draw(shapes.rectangle(0, 0, 160, 120))
    draw_header()

    screen.font = large_font
    screen.brush = white
    center_text("Connecting...", 45)

    dots = "." * (int(io.ticks / 500) % 4)
    screen.font = small_font
    screen.brush = phosphor
    center_text("WiFi%s" % dots, 65)


def draw_wifi_error():
    screen.brush = dark_bg
    screen.draw(shapes.rectangle(0, 0, 160, 120))
    draw_header()

    screen.font = large_font
    screen.brush = white
    center_text("WiFi Failed!", 35)

    screen.font = small_font
    screen.brush = red
    center_text("Could not connect", 55)
    center_text("to WiFi network.", 67)

    screen.brush = faded
    center_text("A: retry connection", 90)


def draw_error_screen():
    screen.brush = dark_bg
    screen.draw(shapes.rectangle(0, 0, 160, 120))
    draw_header()
    draw_flag_selector()

    screen.font = small_font
    screen.brush = red
    lines = error_message.split("\n")
    y = 55
    for line in lines:
        center_text(line, y)
        y += 12

    screen.brush = faded
    center_text("A: retry", 100)
    draw_button_hints()


def draw_idle_screen():
    screen.brush = dark_bg
    screen.draw(shapes.rectangle(0, 0, 160, 120))
    draw_header()
    draw_flag_selector()

    key = FLAG_KEYS[flag_index]
    if key in cache:
        result = cache[key]
        if result["error"]:
            screen.font = small_font
            screen.brush = red
            center_text("Error: %s" % result["error"]["code"], 60)
        else:
            draw_value(result)
    else:
        screen.font = small_font
        screen.brush = faded
        center_text("Press A to evaluate", 60)

    draw_button_hints()


def draw_evaluating():
    screen.brush = dark_bg
    screen.draw(shapes.rectangle(0, 0, 160, 120))
    draw_header()
    draw_flag_selector()

    spinner = shapes.rounded_rectangle(0, 0, 8, 8, 2)
    for i in range(4):
        spinner.transform = (
            Matrix()
            .translate(80, 68)
            .rotate((io.ticks + i * 90) / 5)
            .scale(1.0 + i * 0.3)
        )
        screen.brush = brushes.color(211, 250, 55, 200 - i * 40)
        screen.draw(spinner)

    screen.brush = phosphor
    screen.font = small_font
    center_text("Evaluating...", 88)


# -- Main Update Loop ------------------------------------------------------


def start_evaluation():
    global eval_task, state
    state = EVALUATING
    eval_task = evaluate_flag_gen(FLAG_KEYS[flag_index])


def update():
    global state, flag_index, cache, secrets, error_message, eval_task, raw_scroll

    # -- State machine --
    if state == INIT:
        secrets = load_secrets()
        if secrets is None:
            state = NO_SECRETS
        else:
            state = CONNECTING
        return

    if state == NO_SECRETS:
        draw_no_secrets()
        return

    if state == CONNECTING:
        result = wifi_connect(secrets["ssid"], secrets["password"])
        if result is True:
            state = IDLE
        elif result is False:
            state = WIFI_ERROR
        else:
            draw_connecting()
        return

    if state == WIFI_ERROR:
        draw_wifi_error()
        if io.BUTTON_A in io.pressed:
            wifi_reset()
            state = CONNECTING
        return

    if state == EVALUATING:
        draw_evaluating()
        if eval_task is not None:
            try:
                result = next(eval_task)
                if result is not None:
                    key = FLAG_KEYS[flag_index]
                    cache[key] = result
                    if result["error"]:
                        error_message = "%s\n%s" % (
                            result["error"]["code"],
                            result["error"]["message"],
                        )
                        state = ERROR
                    else:
                        state = DISPLAY
                    eval_task = None
            except StopIteration:
                eval_task = None
                state = IDLE
        return

    # IDLE and DISPLAY share the same input handling and screen
    if state in (IDLE, DISPLAY):
        if io.BUTTON_UP in io.pressed:
            flag_index = (flag_index - 1) % len(FLAG_KEYS)
        if io.BUTTON_DOWN in io.pressed:
            flag_index = (flag_index + 1) % len(FLAG_KEYS)
        if io.BUTTON_B in io.pressed and FLAG_KEYS[flag_index] in cache:
            raw_scroll = 0
            state = RAW_DISPLAY
            return
        if io.BUTTON_A in io.pressed:
            start_evaluation()
            return
        if io.BUTTON_C in io.pressed:
            cache.clear()
            start_evaluation()
            return
        draw_idle_screen()
        return

    if state == RAW_DISPLAY:
        key = FLAG_KEYS[flag_index]
        result = cache.get(key)
        if result:
            lines = _build_detail_lines(result)
            max_scroll = max(0, len(lines) - 7)
        else:
            max_scroll = 0

        if io.BUTTON_UP in io.pressed:
            raw_scroll = max(0, raw_scroll - 1)
        if io.BUTTON_DOWN in io.pressed:
            raw_scroll = min(max_scroll, raw_scroll + 1)
        if io.BUTTON_B in io.pressed:
            state = DISPLAY
            return
        draw_raw_response_screen()
        return

    if state == ERROR:
        draw_error_screen()
        if io.BUTTON_UP in io.pressed:
            flag_index = (flag_index - 1) % len(FLAG_KEYS)
            state = IDLE
        if io.BUTTON_DOWN in io.pressed:
            flag_index = (flag_index + 1) % len(FLAG_KEYS)
            state = IDLE
        if io.BUTTON_A in io.pressed:
            start_evaluation()
        if io.BUTTON_C in io.pressed:
            cache.clear()
            start_evaluation()
        if io.BUTTON_B in io.pressed and FLAG_KEYS[flag_index] in cache:
            raw_scroll = 0
            state = RAW_DISPLAY
        return


if __name__ == "__main__":
    run(update)
