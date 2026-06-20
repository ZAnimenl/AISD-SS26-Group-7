from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_canonical_stack_and_contract_files_exist():
    assert (ROOT / "frontend/index.html").exists()
    assert (ROOT / "frontend/styles.css").exists()
    assert (ROOT / "frontend/app.js").exists()
    assert "FastAPI" in (ROOT / "backend/main.py").read_text(encoding="utf-8")
    assert "SqliteDatabase" in (ROOT / "backend/models.py").read_text(encoding="utf-8")
    assert "class Todo" in (ROOT / "backend/models.py").read_text(encoding="utf-8")


def test_browser_ui_has_no_remote_dependencies():
    html = (ROOT / "frontend/index.html").read_text(encoding="utf-8")
    assert "http://" not in html
    assert "https://" not in html
    assert 'href="styles.css"' in html
    assert 'src="app.js"' in html

