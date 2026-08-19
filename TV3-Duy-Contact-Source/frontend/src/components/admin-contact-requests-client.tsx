"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import {
  ApiError,
  contactRequestsApi,
  type ContactRequestDetail,
  type ContactRequestListItem,
  type ContactRequestStatus,
  type PagedResult,
} from "@/lib/api";
import {
  contactRequestStatusMeta,
  contactRequestStatusValues,
  getContactRequestTransitionHints,
  requiresContactRequestStatusNote,
} from "@/lib/contact-request-status";

const pageSize = 12;

const formatDate = (value: string) => new Intl.DateTimeFormat("vi-VN", {
  dateStyle: "medium", timeStyle: "short"
}).format(new Date(value));

export function AdminContactRequestsClient() {
  const [result, setResult] = useState<PagedResult<ContactRequestListItem> | null>(null);
  const [selected, setSelected] = useState<ContactRequestDetail | null>(null);
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<ContactRequestStatus | "">("");
  const [page, setPage] = useState(1);
  const [note, setNote] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setResult(await contactRequestsApi.all({ page, pageSize, search: search || undefined, status: status || undefined }));
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Không thể tải danh sách yêu cầu liên hệ.");
    } finally {
      setLoading(false);
    }
  }, [page, search, status]);

  useEffect(() => {
    const timer = window.setTimeout(() => { void load(); }, 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const openDetail = async (id: string) => {
    setError("");
    try {
      setSelected(await contactRequestsApi.detail(id));
      setNote("");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Không thể tải chi tiết yêu cầu.");
    }
  };

  const submitFilters = (event: FormEvent) => {
    event.preventDefault();
    setPage(1);
    setSearch(searchDraft.trim());
  };

  const allowedTransitions = selected ? getContactRequestTransitionHints(selected.status) : [];

  const changeStatus = async (next: ContactRequestStatus) => {
    if (!selected) return;
    if (requiresContactRequestStatusNote(next) && !note.trim()) {
      setError("Vui lòng nhập ghi chú khi từ chối hoặc huỷ yêu cầu.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const updated = await contactRequestsApi.updateStatus(selected.id, next, note.trim() || undefined);
      setSelected(updated);
      setNote("");
      setNotice(`Đã cập nhật trạng thái thành “${contactRequestStatusMeta[next].label}”.`);
      await load();
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Không thể cập nhật trạng thái.");
    } finally {
      setSaving(false);
    }
  };

  return <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
    <section className="overflow-hidden rounded-3xl bg-[radial-gradient(circle_at_top_left,_#0ea5e9,_#1e3a8a_55%,_#0f172a)] px-6 py-8 text-white shadow-xl sm:px-9">
      <p className="text-xs font-bold uppercase tracking-[0.22em] text-sky-200">Bảng điều phối tư vấn</p>
      <div className="mt-3 flex flex-col justify-between gap-5 lg:flex-row lg:items-end">
        <div><h1 className="text-3xl font-black tracking-tight sm:text-4xl">Yêu cầu liên hệ</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-200">Theo dõi từng yêu cầu tư vấn, ghi nhận lịch sử xử lý và chỉ chuyển theo workflow đã chốt.</p></div>
        <div className="rounded-2xl border border-white/15 bg-white/10 px-5 py-3 backdrop-blur"><span className="block text-xs text-sky-100">Tổng kết quả</span><strong className="text-2xl">{result?.totalCount ?? "—"}</strong></div>
      </div>
    </section>

    <form className="mt-6 grid gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm md:grid-cols-[1fr_12rem_auto]" onSubmit={submitFilters}>
      <label className="min-w-0"><span className="sr-only">Tìm kiếm yêu cầu</span><input className="h-11 w-full rounded-xl border border-slate-200 px-4 text-sm outline-none transition focus:border-sky-500 focus:ring-4 focus:ring-sky-100" placeholder="Tìm theo tên, email, số điện thoại hoặc chủ đề" value={searchDraft} onChange={event => setSearchDraft(event.target.value)} /></label>
      <label><span className="sr-only">Lọc theo trạng thái</span><select className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none focus:border-sky-500 focus:ring-4 focus:ring-sky-100" value={status} onChange={event => { setStatus(event.target.value ? Number(event.target.value) as ContactRequestStatus : ""); setPage(1); }}><option value="">Tất cả trạng thái</option>{contactRequestStatusValues.map(value => <option key={value} value={value}>{contactRequestStatusMeta[value].label}</option>)}</select></label>
      <button className="h-11 rounded-xl bg-sky-600 px-6 text-sm font-bold text-white transition hover:bg-sky-700 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-sky-200" type="submit">Lọc kết quả</button>
    </form>

    {error && <p className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700" role="alert">{error}</p>}
    {notice && <p className="mt-4 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700" role="status">{notice}</p>}

    <div className="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_25rem]">
      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-100 px-5 py-4"><h2 className="font-bold text-slate-900">Danh sách yêu cầu</h2><p className="mt-1 text-sm text-slate-500">Chọn một dòng để xem nội dung và lịch sử thay đổi.</p></div>
        <div className="overflow-x-auto"><table className="w-full min-w-[44rem] text-left text-sm"><thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-5 py-3">Khách hàng</th><th className="px-4 py-3">Chủ đề</th><th className="px-4 py-3">Trạng thái</th><th className="px-5 py-3">Gửi lúc</th></tr></thead><tbody>{loading ? <tr><td className="px-5 py-8 text-slate-500" colSpan={4}>Đang tải yêu cầu...</td></tr> : result?.items.length ? result.items.map(item => <tr className={`cursor-pointer border-t border-slate-100 transition hover:bg-sky-50 ${selected?.id === item.id ? "bg-sky-50" : ""}`} key={item.id} onClick={() => void openDetail(item.id)}><td className="px-5 py-4"><strong className="block text-slate-900">{item.fullName}</strong><span className="text-xs text-slate-500">{item.email} · {item.phoneNumber}</span></td><td className="px-4 py-4 text-slate-700">{item.subject}</td><td className="px-4 py-4"><StatusBadge status={item.status} /></td><td className="px-5 py-4 text-xs text-slate-500">{formatDate(item.createdAt)}</td></tr>) : <tr><td className="px-5 py-10 text-center text-slate-500" colSpan={4}>Không có yêu cầu phù hợp.</td></tr>}</tbody></table></div>
        {!!result && <nav className="flex items-center justify-between border-t border-slate-100 px-5 py-4 text-sm"><span className="text-slate-500">Trang {result.page} / {Math.max(1, result.totalPages)}</span><div className="flex gap-2"><button className="rounded-lg border border-slate-200 px-3 py-2 disabled:opacity-40" disabled={result.page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))} type="button">Trước</button><button className="rounded-lg border border-slate-200 px-3 py-2 disabled:opacity-40" disabled={result.page >= result.totalPages} onClick={() => setPage(value => value + 1)} type="button">Sau</button></div></nav>}
      </section>

      <aside className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm xl:sticky xl:top-6 xl:self-start">{selected ? <><div className="flex items-start justify-between gap-3"><div><p className="text-xs font-bold uppercase tracking-[0.15em] text-sky-600">Chi tiết yêu cầu</p><h2 className="mt-1 text-xl font-black text-slate-900">{selected.fullName}</h2></div><button className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700" onClick={() => setSelected(null)} type="button" aria-label="Đóng chi tiết">×</button></div><div className="mt-5 space-y-4 text-sm"><Info label="Email" value={selected.email} /><Info label="Điện thoại" value={selected.phoneNumber} /><Info label="Công ty" value={selected.companyName || "—"} /><Info label="Chủ đề" value={selected.subject} /><div><span className="text-xs font-bold uppercase tracking-wide text-slate-400">Nội dung</span><p className="mt-1 whitespace-pre-wrap rounded-xl bg-slate-50 p-3 leading-6 text-slate-700">{selected.message}</p></div><div><span className="text-xs font-bold uppercase tracking-wide text-slate-400">Trạng thái hiện tại</span><div className="mt-2"><StatusBadge status={selected.status} /></div></div>{allowedTransitions.length > 0 && <div className="border-t border-slate-100 pt-4"><label className="block text-xs font-bold uppercase tracking-wide text-slate-400">Ghi chú xử lý</label><textarea className="mt-2 min-h-24 w-full rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-sky-500 focus:ring-4 focus:ring-sky-100" placeholder="Bắt buộc khi từ chối hoặc huỷ..." value={note} onChange={event => setNote(event.target.value)} /><div className="mt-3 grid gap-2">{allowedTransitions.map(next => <button className={`rounded-xl px-4 py-2.5 text-sm font-bold transition ${next === 3 ? "bg-emerald-600 text-white hover:bg-emerald-700" : next === 4 ? "bg-rose-600 text-white hover:bg-rose-700" : next === 5 ? "bg-slate-700 text-white hover:bg-slate-800" : "bg-sky-600 text-white hover:bg-sky-700"}`} disabled={saving} key={next} onClick={() => void changeStatus(next)} type="button">Chuyển thành {contactRequestStatusMeta[next].label}</button>)}</div></div>}<div className="border-t border-slate-100 pt-4"><span className="text-xs font-bold uppercase tracking-wide text-slate-400">Lịch sử xử lý</span><ol className="mt-3 space-y-3">{selected.statusHistory.length ? selected.statusHistory.map(history => <li className="border-l-2 border-sky-200 pl-3" key={history.id}><div className="flex items-center gap-2"><StatusBadge status={history.toStatus} /><time className="text-xs text-slate-400">{formatDate(history.createdAt)}</time></div>{history.note && <p className="mt-1 text-sm text-slate-600">{history.note}</p>}</li>) : <li className="text-sm text-slate-500">Chưa có lịch sử.</li>}</ol></div></div></> : <div className="py-10 text-center"><div className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-sky-50 text-xl text-sky-600">✦</div><h2 className="mt-4 font-bold text-slate-900">Chọn một yêu cầu</h2><p className="mt-2 text-sm leading-6 text-slate-500">Chi tiết khách hàng, ghi chú và timeline xử lý sẽ xuất hiện ở đây.</p></div>}</aside>
    </div>
  </main>;
}

function StatusBadge({ status }: { status: ContactRequestStatus }) {
  const meta = contactRequestStatusMeta[status];
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-bold ring-1 ring-inset ${meta.className}`}>{meta.label}</span>;
}

function Info({ label, value }: { label: string; value: string }) {
  return <div><span className="text-xs font-bold uppercase tracking-wide text-slate-400">{label}</span><p className="mt-1 break-words text-slate-700">{value}</p></div>;
}
