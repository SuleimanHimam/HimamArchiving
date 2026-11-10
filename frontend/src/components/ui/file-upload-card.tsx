import * as React from "react";
import { UploadCloud, X, CheckCircle2, Trash2 } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";

// Define the structure for a file being uploaded
export interface UploadedFile {
  id: string;
  file: File;
  progress: number; // 0-100
  status: "uploading" | "completed" | "error";
}

// Define the props for the component.
// Omit the handlers `motion` redefines with its own signatures (drag + animation),
// so spreading these props onto <motion.div> type-checks.
interface FileUploadCardProps
  extends Omit<
    React.HTMLAttributes<HTMLDivElement>,
    "onDrag" | "onDragStart" | "onDragEnd" | "onAnimationStart"
  > {
  files: UploadedFile[];
  onFilesChange: (files: File[]) => void;
  onFileRemove: (id: string) => void;
  onClose?: () => void;
  /** Optional action rendered in the header, opposite the title (e.g. a scan button). */
  headerAction?: React.ReactNode;
  /** Tighter dropzone for pages that must fit the viewport without scrolling: smaller
   *  padding and icon, and the format hint / browse button dropped (clicking the zone
   *  already opens the picker, so the button is decorative). */
  compact?: boolean;
}

export const FileUploadCard = React.forwardRef<HTMLDivElement, FileUploadCardProps>(
  ({ className, files = [], onFilesChange, onFileRemove, onClose, headerAction, compact = false, ...props }, ref) => {
    const [isDragging, setIsDragging] = React.useState(false);
    const fileInputRef = React.useRef<HTMLInputElement>(null);

    // Handler for drag enter event
    const handleDragEnter = (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.stopPropagation();
      setIsDragging(true);
    };

    // Handler for drag leave event
    const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.stopPropagation();
      setIsDragging(false);
    };

    // Handler for drag over event
    const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.stopPropagation();
    };

    // Handler for drop event
    const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.stopPropagation();
      setIsDragging(false);
      const droppedFiles = Array.from(e.dataTransfer.files);
      if (droppedFiles && droppedFiles.length > 0) {
        onFilesChange(droppedFiles);
      }
    };

    // Handler for file input change
    const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
      const selectedFiles = Array.from(e.target.files || []);
      if (selectedFiles.length > 0) {
        onFilesChange(selectedFiles);
      }
    };

    // Trigger file input click
    const triggerFileSelect = () => fileInputRef.current?.click();

    // Format file size for display
    const formatFileSize = (bytes: number) => {
      if (bytes === 0) return "0 KB";
      const k = 1024;
      const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
    };

    // Animation variants for Motion
    const cardVariants = {
      hidden: { opacity: 0, y: 20 },
      visible: { opacity: 1, y: 0 },
    };

    const fileItemVariants = {
      hidden: { opacity: 0, x: -20 },
      visible: { opacity: 1, x: 0 },
    };

    return (
      <motion.div
        ref={ref}
        variants={cardVariants}
        initial="hidden"
        animate="visible"
        transition={{ duration: 0.3 }}
        className={cn(
          "w-full max-w-lg bg-background rounded-xl border shadow-sm",
          className
        )}
        {...props}
      >
        <div className={compact ? "p-3" : "p-6"}>
          {(headerAction || onClose) && (
            <div className={cn("flex items-center justify-end gap-2", compact ? "mb-2" : "mb-4")}>
              {headerAction}
              {onClose && (
                <Button variant="ghost" size="icon" className="rounded-full w-8 h-8" onClick={onClose}>
                  <X className="w-4 h-4" />
                </Button>
              )}
            </div>
          )}

          <div
            onDragEnter={handleDragEnter}
            onDragLeave={handleDragLeave}
            onDragOver={handleDragOver}
            onDrop={handleDrop}
            onClick={triggerFileSelect}
            className={cn(
              "border-2 border-dashed rounded-lg flex flex-col items-center justify-center text-center transition-colors duration-200 cursor-pointer",
              compact ? "p-4" : "p-8",
              isDragging
                ? "border-primary bg-primary/10"
                : "border-muted-foreground/30 hover:border-primary/50"
            )}
          >
            <input
              ref={fileInputRef}
              type="file"
              multiple
              className="hidden"
              onChange={handleFileSelect}
            />
            <UploadCloud className={cn("text-muted-foreground", compact ? "w-6 h-6 mb-1.5" : "w-10 h-10 mb-4")} />
            <p className={cn("font-semibold text-foreground", compact && "text-sm")}>اسحب وأفلت الملفات هنا أو انقر للاختيار.</p>
            {!compact && (
              <>
                <p className="text-xs text-muted-foreground mt-1">
                  صيغ JPEG وPNG وPDF وMP4، حتى 50 ميغابايت.
                </p>
                <Button variant="outline" size="sm" className="mt-4 pointer-events-none">
                  استعراض الملفات
                </Button>
              </>
            )}
          </div>
        </div>

        {files.length > 0 && (
          <div className="p-6 border-t">
            <ul className="space-y-4">
              <AnimatePresence>
                {files.map((file) => (
                  <motion.li
                    key={file.id}
                    variants={fileItemVariants}
                    initial="hidden"
                    animate="visible"
                    exit="hidden"
                    layout
                    className="flex items-center justify-between"
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 flex items-center justify-center rounded-md bg-muted text-sm font-bold text-muted-foreground">
                        {file.file.type.split("/")[1]?.toUpperCase().substring(0, 3) || "FILE"}
                      </div>
                      <div className="flex-1">
                        <p className="text-sm font-medium text-foreground truncate max-w-[150px] sm:max-w-xs">{file.file.name}</p>
                        <div className="text-xs text-muted-foreground">
                          {file.status === "uploading" && (
                            <span>{formatFileSize((file.file.size * file.progress) / 100)} من {formatFileSize(file.file.size)}</span>
                          )}
                           {file.status === "completed" && (
                            <span>{formatFileSize(file.file.size)}</span>
                          )}
                          <span className="mx-1">•</span>
                          <span className={cn(
                             {"text-primary": file.status === 'uploading'},
                             {"text-green-500": file.status === 'completed'},
                          )}>
                            {file.status === 'uploading' ? 'جارٍ الرفع…' : 'اكتمل'}
                          </span>
                        </div>
                        {file.status === 'uploading' && <Progress value={file.progress} className="h-1.5 mt-1" />}
                      </div>
                    </div>

                     <div className="flex items-center gap-2">
                        {file.status === 'completed' && <CheckCircle2 className="w-5 h-5 text-green-500" />}
                        <Button variant="ghost" size="icon" className="rounded-full w-8 h-8" onClick={() => onFileRemove(file.id)}>
                           {file.status === 'completed' ? <Trash2 className="w-4 h-4" /> : <X className="w-4 h-4" />}
                        </Button>
                     </div>
                  </motion.li>
                ))}
              </AnimatePresence>
            </ul>
          </div>
        )}
      </motion.div>
    );
  }
);
FileUploadCard.displayName = "FileUploadCard";
