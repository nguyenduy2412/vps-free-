import { expect, test, type Page } from "@playwright/test";

const validRequest = {
  fullName: "Nguyen Phuoc Duy",
  phoneNumber: "0901234567",
  email: "duy.e2e@example.test",
  companyName: "CloudServiceStore QA",
  subject: "Tu van ha tang Cloud",
  message: "Toi can tu van giai phap Cloud cho doanh nghiep cua minh.",
};

async function fillContactForm(page: Page) {
  await page.getByLabel(/Họ và tên/).fill(validRequest.fullName);
  await page.getByLabel(/Số điện thoại/).fill(validRequest.phoneNumber);
  await page.getByLabel(/Email/).fill(validRequest.email);
  await page.getByLabel(/Công ty/).fill(validRequest.companyName);
  await page.getByLabel(/Chủ đề/).fill(validRequest.subject);
  await page.getByLabel(/Nội dung cần tư vấn/).fill(validRequest.message);
}

test("public user can submit a valid contact request and see its request id", async ({ page }) => {
  const requestId = "11111111-2222-3333-4444-555555555555";
  await page.route("**/api/v1/contact-requests", async route => {
    expect(route.request().method()).toBe("POST");
    expect(route.request().postDataJSON()).toEqual(validRequest);
    await route.fulfill({
      status: 201,
      contentType: "application/json",
      body: JSON.stringify({ id: requestId, status: 1, createdAt: "2026-08-18T00:00:00Z" }),
    });
  });

  await page.goto("/contact");
  await fillContactForm(page);
  await page.getByRole("button", { name: "Gửi yêu cầu tư vấn" }).click();

  await expect(page.getByText("Đã tiếp nhận")).toBeVisible();
  await expect(page.getByText(requestId)).toBeVisible();
  await page.getByRole("button", { name: "Gửi yêu cầu khác" }).click();
  await expect(page.getByRole("heading", { name: "Cho chúng tôi biết nhu cầu của bạn" })).toBeVisible();
  await expect(page.getByLabel(/Họ và tên/)).toHaveValue("");
});

test("public user sees ProblemDetails message when contact API rejects the request", async ({ page }) => {
  await page.route("**/api/v1/contact-requests", async route => {
    await route.fulfill({
      status: 409,
      contentType: "application/problem+json",
      body: JSON.stringify({ title: "Business conflict", detail: "A contact request with this email was submitted recently." }),
    });
  });

  await page.goto("/contact");
  await fillContactForm(page);
  await page.getByRole("button", { name: "Gửi yêu cầu tư vấn" }).click();

  await expect(page.locator("#contact-error")).toContainText("A contact request with this email was submitted recently.");
  await expect(page.getByRole("button", { name: "Gửi yêu cầu tư vấn" })).toBeEnabled();
});

test("public user sees validation details returned as ProblemDetails errors", async ({ page }) => {
  await page.route("**/api/v1/contact-requests", async route => {
    await route.fulfill({
      status: 400,
      contentType: "application/problem+json",
      body: JSON.stringify({
        title: "Validation failed",
        errors: { subject: ["Subject contains unsupported content."] },
      }),
    });
  });

  await page.goto("/contact");
  await fillContactForm(page);
  await page.getByRole("button", { name: "Gửi yêu cầu tư vấn" }).click();

  await expect(page.locator("#contact-error")).toContainText("Subject contains unsupported content.");
});

test("public user sees a rate-limit message and can retry after a 429 response", async ({ page }) => {
  let attempts = 0;
  await page.route("**/api/v1/contact-requests", async route => {
    attempts += 1;
    await route.fulfill({
      status: 429,
      contentType: "application/problem+json",
      body: JSON.stringify({ title: "Too many requests", detail: "Please wait before submitting another request." }),
    });
  });

  await page.goto("/contact");
  await fillContactForm(page);
  const submit = page.getByRole("button", { name: "Gửi yêu cầu tư vấn" });
  await submit.click();

  await expect(page.locator("#contact-error")).toContainText("Please wait before submitting another request.");
  await expect(submit).toBeEnabled();
  await submit.click();
  await expect.poll(() => attempts).toBe(2);
});

test("native required validation prevents an empty form from calling the Contact API", async ({ page }) => {
  let requestCount = 0;
  await page.route("**/api/v1/contact-requests", async route => {
    requestCount += 1;
    await route.fulfill({ status: 201, contentType: "application/json", body: "{}" });
  });

  await page.goto("/contact");
  await page.getByRole("button", { name: "Gửi yêu cầu tư vấn" }).click();

  await expect.poll(() => requestCount).toBe(0);
  await expect(page.locator("input:invalid, textarea:invalid").first()).toBeVisible();
});

test("native email validation prevents an invalid email from calling the Contact API", async ({ page }) => {
  let requestCount = 0;
  await page.route("**/api/v1/contact-requests", async route => {
    requestCount += 1;
    await route.fulfill({ status: 201, contentType: "application/json", body: "{}" });
  });

  await page.goto("/contact");
  await fillContactForm(page);
  const email = page.getByLabel(/Email/);
  await email.fill("not-an-email");
  await page.getByRole("button", { name: "Gửi yêu cầu tư vấn" }).click();

  await expect.poll(() => requestCount).toBe(0);
  await expect.poll(() => email.evaluate(element => (element as HTMLInputElement).validity.typeMismatch)).toBe(true);
});

test("public user receives the fallback message when the Contact request has a network failure", async ({ page }) => {
  await page.route("**/api/v1/contact-requests", async route => route.abort("failed"));

  await page.goto("/contact");
  await fillContactForm(page);
  const submit = page.getByRole("button", { name: "Gửi yêu cầu tư vấn" });
  await submit.click();

  await expect(page.locator("#contact-error")).toContainText("Không thể gửi yêu cầu. Vui lòng thử lại sau.");
  await expect(submit).toBeEnabled();
});

test("submit button stays disabled while the Contact API request is pending", async ({ page }) => {
  let requestCount = 0;
  let releaseResponse: (() => void) | undefined;
  const responseGate = new Promise<void>(resolve => { releaseResponse = resolve; });
  await page.route("**/api/v1/contact-requests", async route => {
    requestCount += 1;
    await responseGate;
    await route.fulfill({
      status: 201,
      contentType: "application/json",
      body: JSON.stringify({ id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", status: 1, createdAt: "2026-08-18T00:00:00Z" }),
    });
  });

  await page.goto("/contact");
  await fillContactForm(page);
  const submit = page.locator("form button[type='submit']");
  await submit.click();

  await expect.poll(() => requestCount).toBe(1);
  await expect(submit).toBeDisabled();
  await expect(submit).toHaveText("Đang gửi yêu cầu...");

  releaseResponse?.();
  await expect(page.getByText("Đã tiếp nhận")).toBeVisible();
  expect(requestCount).toBe(1);
});

test("contact page has no horizontal overflow at a mobile viewport", async ({ page }) => {
  await page.goto("/contact");
  await expect(page.getByRole("heading", { name: "Bắt đầu cuộc trao đổi về hạ tầng Cloud." })).toBeVisible();

  const dimensions = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.clientWidth);
});
