import { expect, test } from "@playwright/test";

// Evidence-only test. It is intentionally skipped in the normal E2E suite and
// uses a technical mock response; it does not represent a Docker/staging API run.
test.skip(!process.env.CAPTURE_CONTACT_EVIDENCE, "Set CAPTURE_CONTACT_EVIDENCE=1 to generate local UI screenshots.");

const exampleContact = {
  id: "00000000-0000-0000-0000-000000000001",
  fullName: "Yêu cầu kỹ thuật demo",
  email: "contact-evidence@example.test",
  phoneNumber: "0901234567",
  companyName: "CloudServiceStore QA",
  subject: "Kiểm tra giao diện Contact ở 360px",
  status: 1,
  createdAt: "2026-08-19T05:30:00.000Z"
};

const evidenceAdmin = {
  id: "00000000-0000-0000-0000-000000000002",
  email: "admin-evidence@example.test",
  fullName: "Quản trị viên kỹ thuật",
  roles: ["Admin"]
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/auth/refresh", async route => {
    await route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        accessToken: "evidence-only-not-a-real-token",
        user: evidenceAdmin
      })
    });
  });

  await page.route("**/api/v1/auth/me", async route => {
    await route.fulfill({ contentType: "application/json", body: JSON.stringify(evidenceAdmin) });
  });

  await page.route("**/api/v1/contact-requests**", async route => {
    if (route.request().method() !== "GET") return route.continue();
    await route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        items: [exampleContact],
        page: 1,
        pageSize: 12,
        totalCount: 1,
        totalPages: 1
      })
    });
  });
});

test("capture public Contact layout without horizontal page overflow", async ({ page }, testInfo) => {
  await page.goto("/contact");
  await expect(page.getByRole("heading", { name: "Bắt đầu cuộc trao đổi về hạ tầng Cloud." })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

  await page.screenshot({
    path: testInfo.outputPath(`contact-public-${testInfo.project.name}.png`),
    fullPage: true
  });
});

test("capture Admin Contact layout with technical mock data", async ({ page }, testInfo) => {
  await page.goto("/admin/contact-requests");
  await expect(page.getByRole("heading", { name: "Yêu cầu liên hệ" })).toBeVisible();
  await expect(page.getByText(exampleContact.fullName)).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

  await page.screenshot({
    path: testInfo.outputPath(`contact-admin-${testInfo.project.name}.png`),
    fullPage: true
  });
});
