"use client";

import { FormEvent, useState, type ReactNode } from "react";
import { ApiError, contactRequestsApi, type ContactRequestConfirmation } from "@/lib/api";

const initial = { fullName: "", email: "", phoneNumber: "", companyName: "", subject: "", message: "" };

export function ContactPublicClient() {
  const [form, setForm] = useState(initial);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState<ContactRequestConfirmation | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError("");
    try {
      setDone(await contactRequestsApi.create(form));
      setForm(initial);
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Không thể gửi yêu cầu. Vui lòng thử lại sau.");
    } finally {
      setLoading(false);
    }
  }

  return <main className="min-h-[calc(100vh-5rem)] bg-slate-50 py-10 sm:py-16">
    <div className="mx-auto grid max-w-6xl gap-8 px-4 sm:px-6 lg:grid-cols-[.9fr_1.1fr] lg:px-8">
      <section className="relative overflow-hidden rounded-3xl bg-[radial-gradient(circle_at_top_right,_#38bdf8,_#0f3c91_48%,_#0f172a)] p-7 text-white shadow-2xl sm:p-10">
        <div className="relative z-10"><p className="text-xs font-bold uppercase tracking-[.22em] text-sky-200">CloudServiceStore</p><h1 className="mt-4 text-4xl font-black leading-tight tracking-tight">Bắt đầu cuộc trao đổi về hạ tầng Cloud.</h1><p className="mt-5 max-w-md text-sm leading-7 text-slate-200">Gửi nhu cầu của bạn. Đội ngũ tư vấn sẽ ghi nhận, liên hệ và cập nhật trạng thái minh bạch trong quy trình xử lý.</p><dl className="mt-10 grid gap-4"><ContactHighlight title="Phản hồi có theo dõi" description="Mỗi yêu cầu nhận một mã để đội ngũ tiếp nhận và xử lý." /><ContactHighlight title="Thông tin được bảo vệ" description="Chỉ đội ngũ Admin/Editor được phân quyền mới quản lý yêu cầu." /><ContactHighlight title="Tư vấn theo nhu cầu" description="Hạ tầng, VPS, backup hoặc giải pháp cloud cho doanh nghiệp." /></dl></div><div aria-hidden="true" className="absolute -bottom-24 -right-24 h-64 w-64 rounded-full border-[28px] border-white/10" /><div aria-hidden="true" className="absolute right-12 top-12 h-3 w-3 rounded-full bg-sky-300 shadow-[0_0_0_12px_rgba(125,211,252,.12)]" />
      </section>

      <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-xl shadow-slate-200/60 sm:p-9">
        {done ? <div className="flex min-h-[28rem] flex-col justify-center text-center" aria-live="polite"><div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-emerald-100 text-3xl text-emerald-700">✓</div><p className="mt-6 text-sm font-bold uppercase tracking-[.18em] text-emerald-700">Đã tiếp nhận</p><h2 className="mt-2 text-3xl font-black text-slate-900">Cảm ơn bạn đã liên hệ.</h2><p className="mx-auto mt-4 max-w-md text-sm leading-6 text-slate-600">Mã yêu cầu của bạn là <strong className="break-all text-slate-900">{done.id}</strong>. Vui lòng lưu mã này để đối chiếu khi đội ngũ hỗ trợ liên hệ lại.</p><button className="mx-auto mt-8 rounded-xl bg-sky-600 px-6 py-3 text-sm font-bold text-white transition hover:bg-sky-700" onClick={() => setDone(null)} type="button">Gửi yêu cầu khác</button></div> : <><p className="text-xs font-bold uppercase tracking-[.18em] text-sky-600">Form tư vấn</p><h2 className="mt-2 text-3xl font-black tracking-tight text-slate-900">Cho chúng tôi biết nhu cầu của bạn</h2><p className="mt-3 text-sm leading-6 text-slate-600">Các trường có dấu * là bắt buộc. Thông tin cần chính xác để đội ngũ liên hệ lại.</p><form className="mt-7 grid gap-4" onSubmit={submit} aria-describedby="contact-error"><div className="grid gap-4 sm:grid-cols-2"><Field label="Họ và tên" required><input autoComplete="name" className="field" required value={form.fullName} onChange={event => setForm({ ...form, fullName: event.target.value })} /></Field><Field label="Số điện thoại" required><input autoComplete="tel" className="field" required value={form.phoneNumber} onChange={event => setForm({ ...form, phoneNumber: event.target.value })} /></Field></div><div className="grid gap-4 sm:grid-cols-2"><Field label="Email" required><input autoComplete="email" className="field" required type="email" value={form.email} onChange={event => setForm({ ...form, email: event.target.value })} /></Field><Field label="Công ty"><input autoComplete="organization" className="field" value={form.companyName} onChange={event => setForm({ ...form, companyName: event.target.value })} /></Field></div><Field label="Chủ đề" required><input className="field" required value={form.subject} onChange={event => setForm({ ...form, subject: event.target.value })} /></Field><Field label="Nội dung cần tư vấn" required><textarea className="field min-h-32 resize-y" required rows={5} value={form.message} onChange={event => setForm({ ...form, message: event.target.value })} /></Field>{error && <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700" id="contact-error" role="alert">{error}</p>}<button className="mt-2 inline-flex min-h-12 items-center justify-center rounded-xl bg-sky-600 px-6 text-sm font-bold text-white transition hover:bg-sky-700 disabled:cursor-not-allowed disabled:bg-slate-400" disabled={loading} type="submit">{loading ? "Đang gửi yêu cầu..." : "Gửi yêu cầu tư vấn"}</button></form></>}
      </section>
    </div>
  </main>;
}

function Field({ children, label, required = false }: { children: ReactNode; label: string; required?: boolean }) {
  return <label className="grid gap-1.5"><span className="text-sm font-bold text-slate-700">{label}{required && <span className="ml-1 text-rose-500">*</span>}</span>{children}</label>;
}

function ContactHighlight({ description, title }: { title: string; description: string }) {
  return <div className="rounded-2xl border border-white/15 bg-white/10 p-4 backdrop-blur"><dt className="font-bold">{title}</dt><dd className="mt-1 text-sm leading-6 text-slate-200">{description}</dd></div>;
}
