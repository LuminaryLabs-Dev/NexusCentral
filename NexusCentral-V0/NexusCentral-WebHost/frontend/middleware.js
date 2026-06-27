import { NextResponse } from "next/server";

const mobileKey = process.env.NEXUS_WEB_LAB_MOBILE_KEY || "";

function isLoopbackHost(hostname) {
  return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1" || hostname === "[::1]";
}

function requestHost(request) {
  const host = request.headers.get("host") || "";
  return host.split(":")[0] || request.nextUrl.hostname;
}

function hasMobileAccess(request) {
  if (!mobileKey) {
    return true;
  }
  const queryKey = request.nextUrl.searchParams.get("k") || "";
  const cookieKey = request.cookies.get("nexus_web_lab_mobile_key")?.value || "";
  return queryKey === mobileKey || cookieKey === mobileKey;
}

export function middleware(request) {
  const hostname = requestHost(request);
  if (isLoopbackHost(hostname)) {
    return NextResponse.next();
  }

  if (!hasMobileAccess(request)) {
    return new NextResponse("Nexus Web Lab mobile access key required.", {
      status: 401,
      headers: {
        "Content-Type": "text/plain; charset=utf-8",
        "Cache-Control": "no-store",
      },
    });
  }

  const response = NextResponse.next();
  const queryKey = request.nextUrl.searchParams.get("k") || "";
  if (mobileKey && queryKey === mobileKey) {
    response.cookies.set("nexus_web_lab_mobile_key", mobileKey, {
      httpOnly: true,
      sameSite: "strict",
      secure: false,
      path: "/",
      maxAge: 60 * 60 * 8,
    });
  }
  response.headers.set("Cache-Control", "no-store");
  return response;
}

export const config = {
  matcher: ["/", "/api/lab/:path*"],
};
