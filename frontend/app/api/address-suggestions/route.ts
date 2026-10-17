type GeoapifyResult = {
  address_line1?: string;
  formatted?: string;
  housenumber?: string;
  street?: string;
  country_code?: string;
};

export async function GET(request: Request) {
  const apiKey = process.env.GEOAPIFY_API_KEY;
  if (!apiKey) return Response.json({ enabled: false, suggestions: [] });

  const text = new URL(request.url).searchParams.get("text")?.trim() ?? "";
  if (text.length < 3) return Response.json({ enabled: true, suggestions: [] });
  if (text.length > 120) return Response.json({ suggestions: [] }, { status: 400 });

  const params = new URLSearchParams({
    text,
    format: "json",
    filter: "countrycode:us",
    limit: "6",
    apiKey
  });

  try {
    const response = await fetch(`https://api.geoapify.com/v1/geocode/autocomplete?${params}`, { cache: "no-store" });
    if (!response.ok) return Response.json({ suggestions: [] }, { status: 502 });

    const data = await response.json();
    const results: GeoapifyResult[] = Array.isArray(data.results) ? data.results : [];
    const suggestions = results
      .filter((result) => result.country_code === "us" && result.street)
      .map((result) => ({
        line1: [result.housenumber, result.street].filter(Boolean).join(" ") || result.address_line1,
        label: result.formatted || result.address_line1
      }))
      .filter((result) => result.line1 && result.label);

    return Response.json({ enabled: true, suggestions }, { headers: { "Cache-Control": "no-store" } });
  } catch {
    return Response.json({ suggestions: [] }, { status: 502 });
  }
}
