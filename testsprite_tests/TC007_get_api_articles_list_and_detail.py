import requests
from requests.exceptions import RequestException, Timeout

BASE_URL = "http://localhost:5156"
TIMEOUT = 30

def test_get_api_articles_list_and_detail():
    headers = {
        "Accept": "application/json"
    }

    # Step 1: GET /api/articles to retrieve the list of articles
    try:
        resp_list = requests.get(f"{BASE_URL}/api/articles", headers=headers, timeout=TIMEOUT)
    except (RequestException, Timeout) as e:
        assert False, f"Request to GET /api/articles failed: {e}"

    assert resp_list.status_code == 200, f"Expected 200 OK for GET /api/articles but got {resp_list.status_code}"
    try:
        articles = resp_list.json()
    except ValueError:
        assert False, "Response from GET /api/articles is not valid JSON"

    assert isinstance(articles, list), f"Expected list for articles but got {type(articles)}"

    # If no articles, accept empty list and skip detail tests for valid slugs
    if len(articles) == 0:
        return

    # Step 2: For each article in the list, verify GET /api/articles/{slug} returns details with 200
    for article in articles:
        slug = article.get("slug")
        assert slug, f"Article missing 'slug' field: {article}"

        try:
            resp_detail = requests.get(f"{BASE_URL}/api/articles/{slug}", headers=headers, timeout=TIMEOUT)
        except (RequestException, Timeout) as e:
            assert False, f"Request to GET /api/articles/{slug} failed: {e}"

        assert resp_detail.status_code == 200, f"Expected 200 OK for GET /api/articles/{slug} but got {resp_detail.status_code}"

        try:
            detail_data = resp_detail.json()
        except ValueError:
            assert False, f"Response from GET /api/articles/{slug} is not valid JSON"

        # Basic validation of fields (at least slug and content or title)
        assert detail_data.get("slug") == slug, f"Slug mismatch in article detail: expected {slug}, got {detail_data.get('slug')}"
        assert "content" in detail_data or "title" in detail_data, f"Article detail missing 'content' or 'title' fields for slug {slug}"

    # Step 3: Request with an unknown slug to verify 404 response
    unknown_slug = "this-slug-does-not-exist-1234567890"
    try:
        resp_unknown = requests.get(f"{BASE_URL}/api/articles/{unknown_slug}", headers=headers, timeout=TIMEOUT)
    except (RequestException, Timeout) as e:
        assert False, f"Request to GET /api/articles/{unknown_slug} failed: {e}"

    assert resp_unknown.status_code == 404, f"Expected 404 for GET /api/articles/{unknown_slug} but got {resp_unknown.status_code}"

test_get_api_articles_list_and_detail()